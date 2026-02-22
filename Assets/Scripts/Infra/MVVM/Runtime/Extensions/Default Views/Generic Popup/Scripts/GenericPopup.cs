using Scaffold.MVVM;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GenericPopup : UIView<GenericPopupViewController>
{
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private TextMeshProUGUI titleText;

    [SerializeField] private Button closeButton;
    [SerializeField] private Button button;

    private List<Button> buttons = new List<Button>();

    protected override void OnBind()
    {
        base.OnBind();
        contentText.text = viewModel.Content;
        titleText.text = viewModel.Title;

        if (viewModel.ShowClose)
        {
            closeButton.gameObject.SetActive(true);
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
        else
        {
            closeButton.gameObject.SetActive(false);
        }

        button.gameObject.SetActive(true);
        foreach (var option in viewModel.Options)
        {
            var newButton = Instantiate(button, button.transform.parent);
            CreateButton(option, newButton);
            buttons.Add(newButton);
        }
        button.gameObject.SetActive(false);
    }

    private void Close()
    {
        viewModel.Close();
    }

    protected override void OnUnbind()
    {
        base.OnUnbind();
        closeButton.onClick.RemoveListener(Close);
        foreach (var button in buttons)
        {
            Destroy(button.gameObject);
        }
        buttons.Clear();
    }

    private void CreateButton(ButtonOption option, Button newButton)
    {
        newButton.onClick.AddListener(option.Callback.Invoke);
        newButton.GetComponentInChildren<TextMeshProUGUI>().text = option.Key;
    }
}
