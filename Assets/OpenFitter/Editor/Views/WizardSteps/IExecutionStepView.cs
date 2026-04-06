namespace OpenFitter.Editor.Views
{
    /// <summary>
    /// Interface for the Execution step view.
    /// </summary>
    public interface IExecutionStepView
    {
        void SetStatus(string status);
        void SetStatusBadge(string text, string cssClass);
        void SetProgress(float progress, string title);
        void SetCancelButtonEnabled(bool enabled);
        void AppendLog(string log);
        void ClearLog();
        void ScrollLogToBottom();

        event System.Action? OnCancelClicked;
    }
}
