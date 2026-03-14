using ServerBootstrap.Config.Dto;

namespace ServerBootstrap.Config
{
    public sealed class AnnotationParseResult
    {
        public bool Success => Errors.Count == 0;
        public ServerAnnotationsDto? Data { get; init; }
        public List<string> Errors { get; init; } = new();
    }
}
