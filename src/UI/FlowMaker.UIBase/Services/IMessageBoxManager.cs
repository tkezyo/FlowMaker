using FlowMaker.ViewModels;
using ReactiveUI;
using System.Reactive;

namespace FlowMaker.Services
{
    public interface IMessageBoxManager
    {
        Interaction<AlertInfo, Unit> Alert { get; }
        Interaction<ConformInfo, bool> Conform { get; }
        Interaction<ModalInfo, bool> Modals { get; }
        Interaction<OpenFilesInfo, string[]?> OpenFiles { get; }
        Interaction<SaveFilesInfo, string?> SaveFile { get; }
        Interaction<string, string?> SelectFolder { get; }
        Interaction<ModalInfo, bool> Window { get; }
        Interaction<PromptInfo, PromptResult> Prompt { get; }
    }
    public class AlertInfo
    {
        public string? OwnerTitle { get; set; }
        public string? Title { get; set; }
        public  string Message { get; set; }

        public AlertInfo(string message)
        {
            Message = message;
        }
    }
    public class ConformInfo
    {
        public string? OwnerTitle { get; set; }
        public string? Title { get; set; }
        public  string Message { get; set; }

        public ConformInfo(string message)
        {
            Message = message;
        }
    }
    public class OpenFilesInfo
    {
        public string FilterName { get; set; } = "All";
        public string Filter { get; set; } = "*.*";
        public string? Title { get; set; }
        public bool Multiselect { get; set; }
    }

    public class SaveFilesInfo
    {
        public string FilterName { get; set; } = "All";
        public string Filter { get; set; } = "*.*";
        public string? FileName { get; set; }
        public string? DefaultExtension { get; set; }
        public string? Title { get; set; }
    }
    public class PromptInfo
    {
        
        public string? OwnerTitle { get; set; }
        public  string Title { get; set; }

        public PromptInfo(string title)
        {
            Title = title;
        }

        public string? DefautValue { get; set; }
    }
    public class PromptResult
    {
        public bool Ok { get; set; }
        public string? Value { get; set; }
    }
    public class ModalInfo
    {
        public ModalInfo(string title, IFlowMakerRoutableViewModel viewModel, int width = 800, int height = 600)
        {
            Title = title;
            Width = width;
            Height = height;
            ViewModel = viewModel;
        }
        public string? OwnerTitle { get; set; }
        public string? Title { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public IFlowMakerRoutableViewModel ViewModel { get; set; }
    }
}