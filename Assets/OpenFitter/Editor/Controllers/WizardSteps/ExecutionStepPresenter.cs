#nullable enable
using System;
using UnityEditor;
using UnityEngine.UIElements;
using OpenFitter.Editor.Services;
using OpenFitter.Editor.Views;

namespace OpenFitter.Editor
{
    public sealed class ExecutionStepPresenter : WizardStepPresenterBase
    {
        private readonly ExecutionStepView stepView;
        private readonly FittingService fittingService;
        private readonly IOpenFitterEnvironmentService environmentService;
        private readonly ConfigurationService configService;
        private bool elapsedUpdateRegistered;

        public ExecutionStepPresenter(
            OpenFitterState stateService,
            IOpenFitterEnvironmentService environmentService,
            ConfigurationService configService,
            OpenFitterWizardView view,
            OpenFitterWizardPresenter presenter,
            VisualElement parent)
            : base(stateService, view, presenter, parent)
        {
            this.environmentService = environmentService;
            this.configService = configService;

            fittingService = new FittingService(environmentService);

            stepView = new ExecutionStepView(stepContainer);
            BindElements();
        }

        protected override string UxmlPath => ""; // View handles UXML loading

        protected override void BindElements()
        {
            fittingService.OnLogReceived += OnLogReceived;
            fittingService.OnProgressChanged += OnProgressChanged;
            fittingService.OnStatusChanged += OnStatusChanged;
            fittingService.OnStepChanged += OnStepChanged;
            fittingService.OnStateChanged += OnRunnerStateChanged;
            stepView.OnCancelClicked += OnCancelClicked;
        }

        protected override void UnbindElements()
        {
            fittingService.OnLogReceived -= OnLogReceived;
            fittingService.OnProgressChanged -= OnProgressChanged;
            fittingService.OnStatusChanged -= OnStatusChanged;
            fittingService.OnStepChanged -= OnStepChanged;
            fittingService.OnStateChanged -= OnRunnerStateChanged;
            stepView.OnCancelClicked -= OnCancelClicked;
            UnregisterElapsedUpdate();
        }

        public override void OnEnter()
        {
            base.OnEnter();
            SyncElapsedUpdateRegistration();

            if (CanExecuteFitting() && !fittingService.IsFitting)
            {
                ExecuteFitting();
            }

            Refresh();
        }

        public override bool CanProceed()
        {
            return !fittingService.IsFitting;
        }

        public override void Refresh()
        {
            stepView.SetStatus(string.Format(I18n.Tr("Status: {0}"), fittingService.LastRunSummary));
            UpdateElapsedDisplay();
            UpdateStatusBadge();
            UpdateCancelButtonState();
        }

        private void OnLogReceived(string log)
        {
            stepView.AppendLog(log);
            stepView.ScrollLogToBottom();
        }

        private void OnProgressChanged(float progress, string statusDetail)
        {
            if (progress > 0)
            {
                stepView.SetProgress(progress * 100f, $"{(int)(progress * 100f)}%");
            }

            if (!string.IsNullOrEmpty(statusDetail))
            {
                stepView.SetStatus($"Status: {statusDetail}");
            }
        }

        private void OnStatusChanged(string status)
        {
            UpdateElapsedDisplay();
            UpdateStatusBadge();
            UpdateCancelButtonState();
            InvokeStatusChanged();

            if (status == "Completed")
            {
                stepView.SetProgress(100, "100%");
                stateService.LastOutputPath = fittingService.LastOutputPath;
            }
        }

        private void OnRunnerStateChanged()
        {
            SyncElapsedUpdateRegistration();
            UpdateElapsedDisplay();
        }

        private void OnStepChanged(int current, int total)
        {
            // Future: Update UI with step info
        }

        private void UpdateStatusBadge()
        {
            var state = FittingProgressParser.AnalyzeExecutionStateStatic(fittingService.IsFitting, fittingService.LastRunSummary);

            switch (state)
            {
                case FittingExecutionState.Processing:
                    stepView.SetStatusBadge(I18n.Tr("Processing..."), "processing");
                    break;
                case FittingExecutionState.Completed:
                    stepView.SetStatusBadge(I18n.Tr("Completed"), "completed");
                    break;
                case FittingExecutionState.Error:
                    stepView.SetStatusBadge(I18n.Tr("Error"), "failed");
                    break;
                case FittingExecutionState.Cancelled:
                    stepView.SetStatusBadge(I18n.Tr("Cancelled"), "cancelled");
                    break;
                case FittingExecutionState.Idle:
                default:
                    stepView.SetStatusBadge(I18n.Tr("Idle"), "");
                    break;
            }
        }

        private void UpdateCancelButtonState()
        {
            stepView.SetCancelButtonEnabled(fittingService.IsFitting);
        }

        private void OnCancelClicked()
        {
            if (!fittingService.IsFitting)
            {
                return;
            }

            fittingService.CancelFitting();
            stepView.SetCancelButtonEnabled(false);
        }

        private void SyncElapsedUpdateRegistration()
        {
            if (fittingService.IsFitting)
            {
                RegisterElapsedUpdate();
            }
            else
            {
                UnregisterElapsedUpdate();
            }
        }

        private void RegisterElapsedUpdate()
        {
            if (elapsedUpdateRegistered)
            {
                return;
            }

            EditorApplication.update += OnEditorUpdate;
            elapsedUpdateRegistered = true;
        }

        private void UnregisterElapsedUpdate()
        {
            if (!elapsedUpdateRegistered)
            {
                return;
            }

            EditorApplication.update -= OnEditorUpdate;
            elapsedUpdateRegistered = false;
        }

        private void OnEditorUpdate()
        {
            UpdateElapsedDisplay();
            if (!fittingService.IsFitting)
            {
                UnregisterElapsedUpdate();
            }
        }

        private void UpdateElapsedDisplay()
        {
            var elapsed = fittingService.IsFitting ? fittingService.CurrentElapsed : fittingService.LastRunElapsed;
            stepView.SetElapsedTime(string.Format(I18n.Tr("Elapsed: {0}"), FormatElapsed(elapsed)));
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            int totalHours = (int)elapsed.TotalHours;
            if (totalHours > 0)
            {
                return $"{totalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }

            return $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        public override void Dispose()
        {
            UnregisterElapsedUpdate();
            if (fittingService.IsFitting)
            {
                fittingService.CancelFitting();
            }
            fittingService.Dispose();
            base.Dispose();
        }

        private bool CanExecuteFitting()
        {
            return environmentService.IsEnvironmentReady()
                   && !string.IsNullOrEmpty(stateService.SourceConfigPath)
                   && !string.IsNullOrEmpty(stateService.TargetConfigPath);
        }

        private void ExecuteFitting()
        {
            if (!environmentService.ValidateEnvironmentForRun())
            {
                return;
            }

            fittingService.ExecuteFitting(stateService, stateService.BlendShapeEntries, configService.AvailableConfigs);

            stateService.LastOutputPath = fittingService.LastOutputPath;
        }
    }
}
