using Space.Abstraction.Attributes;

namespace InMemoryCache.Tests;

public class AttributesTests
{
    [Fact]
    public void HandleAttribute_Should_Set_Name_Property()
    {
        var attr = new HandleAttribute { Name = "TestHandle" };
        Assert.Equal("TestHandle", attr.Name);
    }

    [Fact]
    public void PipelineAttribute_Default_Order_Is_100()
    {
        var attr = new PipelineAttribute();
        Assert.Equal(100, attr.Order);
    }

    [Fact]
    public void PipelineAttribute_HandleName_CanBeSet()
    {
        var attr = new PipelineAttribute("TestHandle");
        Assert.Equal("TestHandle", attr.HandleName);
    }
}
