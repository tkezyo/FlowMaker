﻿using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace FlowMaker.ViewModels
{
    public class PromptDialogViewModel : RoutableViewModelBase
    {
        public PromptDialogViewModel()
        {
            OKCommand = ReactiveCommand.Create<bool>(OKCommandExecute);
        }
        [Reactive]
        public string? Title { get; set; }
        [Reactive]
        public string? DefautValue { get; set; }
        [Reactive]
        public bool OK { get; set; }
        public ReactiveCommand<bool, Unit> OKCommand { get; }
        public void OKCommandExecute(bool obj)
        {
            OK = obj;
            CloseModal(OK);
        }
    }
}
