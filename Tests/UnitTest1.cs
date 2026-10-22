namespace Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var num1 = 3;
        var num2 = 3;
        var result = num1 + num2;

        var expected = 6;

        Assert.Equal(expected, result);
    }
}
