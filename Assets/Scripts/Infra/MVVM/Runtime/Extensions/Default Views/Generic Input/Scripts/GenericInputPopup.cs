using Scaffold.MVVM;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.MVVM.Extensions.DefaultViews
{
    public class GenericInputPopup : UIView<GenericInputPopupController>
    {
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI description;
        [SerializeField] private TextMeshProUGUI extraDescription;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button confirmButton;

        protected override void OnOpen()
        {
            base.OnOpen();
            title.text = viewModel.Title;
            description.text = viewModel.Description;
            bool showExtra = !string.IsNullOrEmpty(viewModel.ExtraDescription);
            extraDescription.gameObject.SetActive(showExtra);
            if (showExtra)
            {
                extraDescription.text = viewModel.ExtraDescription;
            }

            if (!string.IsNullOrEmpty(viewModel.Placeholder))
            {
                (input.placeholder as TextMeshProUGUI).text = viewModel.Placeholder;
            }

            confirmButton.onClick.AddListener(Confirm);
            input.onValueChanged.AddListener(ValidateInput);
        }

        private void ValidateInput(string input)
        {
            confirmButton.interactable = viewModel.ValidateInput(input);
        }

        private void Confirm()
        {
            viewModel.TryConfirmInput(input.text);
        }

        protected override void OnClose()
        {
            confirmButton.onClick.RemoveListener(Confirm);
            input.onValueChanged.RemoveListener(ValidateInput);
            base.OnClose();
        }

        public void CloseView()
        {
            viewModel.TryConfirmInput(null);
        }
    }
}
