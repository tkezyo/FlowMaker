namespace FlowMaker.Persistence.EntityFramework.Models;

public class FlowDefinitionModel
{
    public Guid Id { get; set; }
    public required string Category { get; set; }
    public required string Name { get; set; }

    public string? Data { get; set; }
}
