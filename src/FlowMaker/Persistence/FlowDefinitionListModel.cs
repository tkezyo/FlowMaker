using Ty;

namespace FlowMaker.Persistence
{
    public class FlowDefinitionListModel
    {
        public Guid Id { get; set; }
        public required string Category { get; set; }
        public required string Name { get; set; }
        public List<NameValue<Guid>> Configs { get; set; } = [];
    }
}
