using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Npgsql;
using Dapper;
using System.Windows.Media.Media3D;
using System.Net.Http;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Xml.Linq;

namespace MyWpfApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public interface ICadDocumentReader
{
    Task<CadDocument> ReadAsync(string path, CancellationToken ct);
}
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // string htmlContent = """
        //         <html lang="en">
        //         <head>
        //             <meta charset="UTF-8">
        //             <title>Document</title>
        //             <style>
        //                 h1 { color: red; }
        //             </style>
        //         </head>
        //         <body>
        //             <h1>Hello!</h1>
        //         </body>
        //         </html>
        //         """;

        // MyBrowser.NavigateToString(htmlContent);
    }
    public async Task ReadAsync(string path, CancellationToken ct)
    {
        using (var folder = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Required))
        {

        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        // string path = "cad-export";
        // CancellationToken ct = new CancellationToken();
        // try
        // {
        //     await ReadAsync(path, ct);
        // }
        // catch (Exception ex)
        // {
        //     MessageBox.Show(ex.Message);
        // }
        int SuccessFiles = 0;
        int WarningFiles = 0;
        int ErrorFiles = 0;
        ObservableCollection<WindowOtchet> otchets = new ObservableCollection<WindowOtchet>();

        string folderPath = "cad-export";
        int objectId = 1;
        string[] extensions = { "*.m3d", "*.a3d" };
        foreach (var ext in extensions)
        {
            foreach (string filePath in Directory.EnumerateFiles(folderPath, ext))
            {
                try
                {
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        string content = reader.ReadToEnd();

                        using (JsonDocument doc = JsonDocument.Parse(content))
                        {
                            JsonElement root = doc.RootElement;
                            // string name = root.GetProperty("name").GetString();
                            // MessageBox.Show($"File: {name}");
                            var confConn = "Host=127.0.0.1;Port=5432;Database=wpf;Username=postgres;Password=root";
                            var query = new NpgsqlConnection(confConn);
                            // 1. Задаем версию по умолчанию
                            int formatVersion = 1;

                            // 2. Безопасно читаем (если null, останется 1)
                            if (root.TryGetProperty("formatVersion", out JsonElement fvElement))
                            {
                                fvElement.TryGetInt32(out formatVersion);
                            }
                            string sqlInsertObject = @"
                        INSERT INTO public.pdm_object(object_type, designation, name, current_version_id)
                        VALUES (@object_type, @designation, @name, @current_version_id) RETURNING id";
                            var result = query.ExecuteScalar<int>(sqlInsertObject,
                            new
                            {
                                object_type = root.GetProperty("type").GetString(),
                                designation = root.GetProperty("designation").GetString(),
                                name = root.GetProperty("name").GetString(),
                                current_version_id = formatVersion,
                            });
                            string material = "0";
                            double mass = 0.0;
                            if (root.TryGetProperty("properties", out JsonElement propsElement))
                            {
                                if (propsElement.TryGetProperty("material", out JsonElement matElement))
                                {
                                    material = matElement.GetString();
                                }
                            }
                            if (root.TryGetProperty("properties", out JsonElement propsElement2))
                            {
                                if (propsElement.TryGetProperty("mass", out JsonElement massElement))
                                {
                                    if (massElement.ValueKind == JsonValueKind.Number)
                                    {
                                        mass = massElement.GetDouble();
                                    }
                                }
                            }
                            if (root.TryGetProperty("components", out JsonElement componentsElement))
                            {
                                if (componentsElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (JsonElement comp in componentsElement.EnumerateArray())
                                    {
                                        string fileName = "Неизвестно";
                                        if (comp.TryGetProperty("file", out JsonElement fileElement))
                                        {
                                            fileName = fileElement.GetString() ?? "";
                                        }

                                        int count = 1;
                                        if (comp.TryGetProperty("count", out JsonElement countElement) && countElement.ValueKind == JsonValueKind.Number)
                                        {
                                            count = countElement.GetInt32();
                                        }
                                        MyList.Items.Add($"\t{fileName} (Кол-во: {count})");
                                    }
                                }
                            }

                            var result2 = query.Execute("INSERT INTO public.object_version(id, object_id, version_no, state, material, mass_kg, created_at)"
                            + "VALUES(@id,@object_id,@version_no,@state,@material,@mass_kg,@created_at)",
                            new
                            {
                                id = objectId++,
                                object_id = result,
                                version_no = formatVersion,
                                state = "В работе",
                                material = material,
                                mass_kg = mass == null ? 0.0 : mass,
                                created_at = DateTime.Now.ToString(),
                            });

                            MyList.Items.Add(root.GetProperty("name").GetString());
                            MyListRight.Items.Add(root.GetProperty("designation").GetString());
                            MyListRight.Items.Add(root.GetProperty("name").GetString());
                            MyListRight.Items.Add(root.GetProperty("type").GetString());
                            if (root.GetProperty("formatVersion").GetInt32() == 1)
                            {
                                MyListRight.Items.Add("В работе. Текущая версия:" + root.GetProperty("formatVersion").GetInt32() + " изменяется.");
                            }
                            else if (root.GetProperty("formatVersion").GetInt32() == 2)
                            {
                                MyListRight.Items.Add("Версия:" + root.GetProperty("formatVersion") + ". Изменять нельзя.");
                            }
                            else
                            {
                                MyListRight.Items.Add(root.GetProperty("formatVersion").GetInt32());
                            }
                            MyListRight.Items.Clear();
                        }
                    }
                    SuccessFiles++;
                }
                catch (System.Text.Json.JsonException ex)
                {
                    ErrorFiles++;
                    FileInfo fileInfo = new FileInfo(filePath);
                    string fileName = fileInfo.Name;
                    otchets.Add(new WindowOtchet { File = fileName, Result = "Ошибка", Reason = ex.Message });
                    // MessageBox.Show(table);
                }
                catch (Exception ex)
                {
                    ErrorFiles++;
                    // MessageBox.Show("Error " + ex.ToString());
                }
            }
        }

        string folderPath2 = "cad-export-v2";
        try
        {
            //npgsql
            var conf = "Host=127.0.0.1;Port=5432;Database=wpf;Username=postgres;Password=root";
            var conn = new NpgsqlConnection(conf);
            //dapper
            var result = conn.Query<string>("SELECT name FROM public.pdm_object").ToHashSet();
            foreach (var ext in extensions)
            {
                foreach (string filePath in Directory.EnumerateFiles(folderPath2, ext))
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(filePath))
                        {
                            string content = reader.ReadToEnd();
                            using (JsonDocument doc = JsonDocument.Parse(content))
                            {
                                JsonElement root = doc.RootElement;

                                string currentName = root.GetProperty("name").GetString() ?? "";

                                if (result.Contains(currentName))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Уже существует: {currentName}");
                                }
                                else
                                {
                                    var pdm_object_insert_detal = conn.Execute(@"
                        INSERT INTO public.pdm_object(object_type, designation, name, current_version_id)
                        VALUES (@object_type, @designation, @name, @current_version_id)",
                            new
                            {
                                object_type = root.GetProperty("type").ToString(),
                                designation = root.GetProperty("designation").ToString(),
                                name = root.GetProperty("name").ToString(),
                                current_version_id = root.GetProperty("formatVersion").GetInt32()
                            });
                                    result.Add(currentName);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // MessageBox.Show(ex.ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }

        Window reportWindow = new Window();

        reportWindow.Title = "Отчет об импорте";
        reportWindow.Width = 400;
        reportWindow.Height = 350;
        reportWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        StackPanel MyPanel = new StackPanel();

        DataGrid MyGrid = new DataGrid();
        MyGrid.AutoGenerateColumns = true;
        MyGrid.Height = 300;

        ObservableCollection<WindowNumberOtchet> number_otchets = new ObservableCollection<WindowNumberOtchet>();

        TextBlock myTextBlock = new TextBlock();
        myTextBlock.Text = $"Успешно: {SuccessFiles} Предупреждение: {WarningFiles} Ошибочные: {ErrorFiles}";
        myTextBlock.Margin = new Thickness(0, 0, 0, 10);

        MyGrid.ItemsSource = otchets;

        MyPanel.Children.Add(myTextBlock);
        MyPanel.Children.Add(MyGrid);
        reportWindow.Content = MyPanel;

        reportWindow.ShowDialog();
    }
    private void MyListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var objectId = 1;
        MyListRight.Items.Clear();
        string selectedName = MyList.SelectedItem as string;

        var dbConf = "Host=127.0.0.1;Port=5432;Database=wpf;Username=postgres;Password=root";
        using var query = new NpgsqlConnection(dbConf);
        var result = query.QueryFirstOrDefault<pdm_object>("SELECT * FROM public.pdm_object WHERE name = @name LIMIT 1",
        new { name = selectedName });

        try
        {
            if (result != null)
            {
                var object_detal = query.QueryFirstOrDefault<object_version>("SELECT * FROM public.object_version WHERE object_id = @object_id LIMIT 1",
                new { object_id = result.id });

                CancelButton.IsEnabled = (result.current_version_id != 3);
                ApproveButton.IsEnabled = (result.current_version_id != 3);
                MyListRight.Items.Add("Наименование: " + result.name);
                MyListRight.Items.Add("Обозначение: " + result.designation);
                MyListRight.Items.Add("Тип: " + result.object_type);
                MyListRight.Items.Add("Версия: " + result.current_version_id);
                if (object_detal != null)
                {
                    // MyListRight.Items.Add("Материал: " + object_detal.material.ToString());
                    MyListRight.Items.Add("Масса: " + object_detal.mass_kg.ToString("F4") + " кг");
                }
                if (result.current_version_id == 1)
                {
                    MyListRight.Items.Add("Соятояние: В работе");
                }
                else if (result.current_version_id == 2)
                {
                    MyListRight.Items.Add("Состояние: Утверждено");
                }
                else
                {
                    MyListRight.Items.Add("Состояние: Аннулировано");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
    private void Approve_Button(object sender, EventArgs e)
    {
        if (MyList.SelectedItem != null)
        {
            string selectedName = MyList.SelectedItem.ToString();

            MessageBox.Show(selectedName);
            var conf = "Host=127.0.0.1;Port=5432;Database=wpf;Username=postgres;Password=root";
            var conn = new NpgsqlConnection(conf);
            var result = conn.QueryFirstOrDefault<pdm_object>("UPDATE public.pdm_object SET current_version_id=@current_version_id WHERE name=@name",
            new { current_version_id = 2, name = selectedName });
        }
    }
    private void Cancel_Button(object sender, EventArgs e)
    {
        if (MyList.SelectedItem != null)
        {
            CancelButton.IsEnabled = true;
            string selectedName = MyList.SelectedItem.ToString();

            MessageBox.Show(selectedName);
            var conf = "Host=127.0.0.1;Port=5432;Database=wpf;Username=postgres;Password=root";
            var conn = new NpgsqlConnection(conf);
            var rowsAffected = conn.Execute("UPDATE public.pdm_object SET current_version_id=@current_version_id WHERE name=@name",
            new { current_version_id = 3, name = selectedName });
            if (rowsAffected > 0)
            {
                CancelButton.IsEnabled = false;
                ApproveButton.IsEnabled = false;
            }
            else
            {
                MessageBox.Show($"В базе нет детали: {selectedName}");
                CancelButton.IsEnabled = true;
                ApproveButton.IsEnabled = true;
            }
        }
    }

    // private void MyListRight_SelectionChanged(object sender, SelectionChangedEventArgs e)
    // {

    //     if (MyComboBox.SelectedItem != null)
    //     {
    //         string selectedName = MyListRight.SelectedItem as string;
    //         selectedName = selectedName.Trim();

    //         var comboItem = MyComboBox.SelectedItem as ComboBoxItem;
    //         string selectedValue = comboItem?.Content?.ToString();

    //         var conf = "Host=127.0.0.1;Port=5432;Database=wpf;Username=postgres;Password=root";
    //         try
    //         {
    //             MessageBox.Show(selectedValue);
    //             if (selectedValue == "В работе")
    //             {
    //                 var query = new NpgsqlConnection(conf);
    //                 var result = query.Execute("UPDATE public.pdm_object SET current_version_id=@id WHERE name=@name;",
    //                 new { id = 1, name = selectedName });
    //             }
    //             else if (selectedValue == "Утверждено")
    //             {
    //                 var query = new NpgsqlConnection(conf);
    //                 var result = query.Execute("UPDATE public.pdm_object SET current_version_id=@id WHERE name=@name;",
    //                 new { id = 2, name = selectedName });
    //             }
    //         }
    //         catch (Exception ex)
    //         {
    //             MessageBox.Show(ex.Message);
    //         }
    //     }
    // }
}
public class CadDocument
{

}
class pdm_object
{
    public int id { get; set; }
    public string object_type { get; set; }
    public string designation { get; set; }
    public string name { get; set; }
    public int current_version_id { get; set; }
}
class object_version
{
    public int id { get; set; }
    public int object_id { get; set; }
    public int version_no { get; set; }
    public string state { get; set; }
    public string material { get; set; }
    public double mass_kg { get; set; }
    public string created_at { get; set; }
}
class bom_link
{
    public int id { get; set; }
    public int parent_version_id { get; set; }
    public int child_object_id { get; set; }
    public int quantity { get; set; }
}
class import_log
{
    public int id { get; set; }
    public int started_at { get; set; }
    public string file_name { get; set; }
    public string severity { get; set; }
    public string reason { get; set; }
}

class WindowNumberOtchet
{
    public int sf { get; set; }
    public int wf { get; set; }
    public int ef { get; set; }
}
class WindowOtchet
{
    public string File { get; set; }
    public string Result { get; set; }
    public string Reason { get; set; }
}