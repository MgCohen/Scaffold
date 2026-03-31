using System.Collections.Generic;
using Scaffold.MVVM.Binding;
using Scaffold.MVVM.Contracts;
using Scaffold.Navigation.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.MVVM
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    public class UIView<T> : View<T> where T : IViewModel
    {
        [SerializeField, HideInInspector] protected Canvas canvas;
        [SerializeField] protected ViewScaling viewScale = ViewScaling.HeightFirst;

        protected override void Order(int order)
        {
            base.Order(order);
            SetCanvas();
            canvas.sortingOrder = order;
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            SetCanvas();
        }

        private void SetCanvas()
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }
            if (canvas != null && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }
            if (canvas != null)
            {
                SetCanvasScale();
            }
        }

        private void SetCanvasScale()
        {
            Vector2 rect = (transform as RectTransform).rect.size;
            bool matchHeight = (rect.x / rect.y) > 0.6f;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight = viewScale switch
            {
                ViewScaling.WidthFirst => matchHeight ? 0 : 1,
                ViewScaling.HeightFirst => matchHeight ? 1 : 0,
                ViewScaling.MatchWidth => 1,
                ViewScaling.MatchHeight => 0,
                _ => 0,
            };
        }
    }
}
