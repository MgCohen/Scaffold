using Scaffold.MVVM;
using TMPro;
using UnityEngine;

public class SampleView : UIView<SampleViewModel>
{
    [SerializeField] private TextMeshProUGUI counterText;
    [SerializeField] private TextMeshProUGUI sampleText;

    protected override void OnBind()
    {
        base.OnBind();
        Bind(() => viewModel.Sample, () => sampleText.text);
        Bind(() => viewModel.Parent.Child.Value, () => counterText.text);
        Bind<int, int>(() => viewModel.Parent.Child.Value, Yell);
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        Debug.Log(viewModel);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            //viewModel.Pass();
            viewModel.Parent.Child.Value++;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            viewModel.Parent.NewChild();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            viewModel.NewParent();
        }
    }

    public void Yell(int value)
    {
        Debug.Log("Value: " + value);
    }
}
