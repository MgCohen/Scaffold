using UnityEngine;
using TMPro;
using Scaffold.MVVM;
using UnityEngine.UI;

namespace Madbox.App.MainMenu
{
    public sealed class MainMenuView : UIView<MainMenuViewController>
    {
        [SerializeField] private TextMeshProUGUI currentLevelLabel;
        [SerializeField] private TextMeshProUGUI currentGoldLabel;
        [SerializeField] private TextMeshProUGUI levelStateLabel;
        [SerializeField] private Button startLevelButton;
        [SerializeField] private Button finishLevelButton;

        protected override void OnBind()
        {
            base.OnBind();
            RegisterLabelBindings();
            RegisterStateBindings();
            RegisterButtonListeners();
        }

        protected override void OnUnbind()
        {
            UnregisterButtonListeners();
            base.OnUnbind();
        }

        private void RegisterLabelBindings()
        {
            RegisterLevelLabelBinding();
            RegisterGoldLabelBinding();
            RegisterStateLabelBinding();
        }

        private void RegisterStateBindings()
        {
            RegisterStartButtonStateBinding();
            RegisterFinishButtonStateBinding();
        }

        private void RegisterLevelLabelBinding()
        {
            if (currentLevelLabel == null) { return; }
            Bind<string, string>(() => viewModel.CurrentLevelLabel, () => currentLevelLabel.text);
        }

        private void RegisterGoldLabelBinding()
        {
            if (currentGoldLabel == null) { return; }
            Bind<string, string>(() => viewModel.CurrentGoldLabel, () => currentGoldLabel.text);
        }

        private void RegisterStateLabelBinding()
        {
            if (levelStateLabel == null) { return; }
            Bind<string, string>(() => viewModel.LevelStateLabel, () => levelStateLabel.text);
        }

        private void RegisterStartButtonStateBinding()
        {
            if (startLevelButton == null) { return; }
            Bind<bool, bool>(() => viewModel.CanStartLevel, () => startLevelButton.interactable);
        }

        private void RegisterFinishButtonStateBinding()
        {
            if (finishLevelButton == null) { return; }
            Bind<bool, bool>(() => viewModel.CanFinishLevel, () => finishLevelButton.interactable);
        }

        private void RegisterButtonListeners()
        {
            RegisterStartButtonListener();
            RegisterFinishButtonListener();
        }

        private void RegisterStartButtonListener()
        {
            if (startLevelButton == null) { return; }
            startLevelButton.onClick.RemoveListener(viewModel.StartLevel);
            startLevelButton.onClick.AddListener(viewModel.StartLevel);
        }

        private void RegisterFinishButtonListener()
        {
            if (finishLevelButton == null) { return; }
            finishLevelButton.onClick.RemoveListener(viewModel.FinishLevel);
            finishLevelButton.onClick.AddListener(viewModel.FinishLevel);
        }

        private void UnregisterButtonListeners()
        {
            UnregisterStartButtonListener();
            UnregisterFinishButtonListener();
        }

        private void UnregisterStartButtonListener()
        {
            if (startLevelButton == null) { return; }
            startLevelButton.onClick.RemoveListener(viewModel.StartLevel);
        }

        private void UnregisterFinishButtonListener()
        {
            if (finishLevelButton == null) { return; }
            finishLevelButton.onClick.RemoveListener(viewModel.FinishLevel);
        }
    }
}
