using ReactiveUI;
using System.Reactive;
using Ty.ViewModels;

namespace Ty.Services
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
    public class AlertInfo(string message)
    {
        public string? OwnerTitle { get; set; }
        public string? Title { get; set; }
        public string Message { get; set; } = message;
    }
    public class ConformInfo(string message)
    {
        public string? OwnerTitle { get; set; }
        public string? Title { get; set; }
        public string Message { get; set; } = message;
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
    public class PromptInfo(string title)
    {

        public string? OwnerTitle { get; set; }
        public string Title { get; set; } = title;

        public string? DefaultValue { get; set; }
    }
    public class PromptResult
    {
        public bool Ok { get; set; }
        public string? Value { get; set; }
    }
    public class ModalInfo(string title, ITyRoutableViewModel viewModel, int width = 800, int height = 600)
    {
        public bool OnlyOne { get; set; }
        public string? OwnerTitle { get; set; }
        public string? Title { get; set; } = title;
        public int Width { get; set; } = width;
        public int Height { get; set; } = height;
        public ITyRoutableViewModel ViewModel { get; set; } = viewModel;
    }
}