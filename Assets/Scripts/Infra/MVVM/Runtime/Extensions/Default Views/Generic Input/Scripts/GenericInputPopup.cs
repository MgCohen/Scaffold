using Scaffold.MVVM;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        if(showExtra)
            extraDescription.text = viewModel.ExtraDescription;
        
        if (!string.IsNullOrEmpty(viewModel.Placeholder))
        {
            (input.placeholder as TextMeshProUGUI).text = viewModel.Placeholder;
        }

        confirmButton.onClick.AddListener(Confirm);
        input.onValueChanged.AddListener(ValidateInput);
    }

    private void ValidateInput(string input)
    {
        //probably should be binded to a property on the controller and propage automatically
        confirmButton.interactable = viewModel.ValidateInput(input);
    }

    private void Confirm()
    {
        //errors should be handled somehow in the future - probably by binding a command or list of errors
        viewModel.TryConfirmInput(input.text);
        //base.CloseView();
    }

    protected override void OnClose()
    {
        confirmButton.onClick.RemoveListener(Confirm);
        input.onValueChanged.RemoveListener(ValidateInput);
        base.OnClose();
    }

    public /*override */void CloseView()
    {
        viewModel.TryConfirmInput(null);
        //base.CloseView();
    }
}
