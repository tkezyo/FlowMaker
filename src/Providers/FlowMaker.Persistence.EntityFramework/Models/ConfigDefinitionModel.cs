namespace FlowMaker.Persistence.EntityFramework.Models;

public class ConfigDefinitionModel
{
    public Guid Id { get; set; }
    public Guid FlowId { get; set; }
    public required string Name { get; set; }

    public string? Data { get; set; }
}
