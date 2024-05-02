﻿using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Source.Features.SceneEditor.UI.SavePanel
{
    public class LoadPanelViewController
    {
        public event Action Opened;
        public event Action Closed;
        public event Action<string> LoadButtonClicked;
        
        private readonly LoadPanelView _view;
        private readonly Button _openButton;

        public LoadPanelViewController(LoadPanelView view)
        {
            _view = view;
            _openButton = _view.GetShowButton();
            
            var loadButton = _view.GetLoadButton();
            var closeButton = _view.GetCloseButton();
            
            _openButton.onClick.AddListener(OnShowButtonClicked);
            loadButton.onClick.AddListener(OnLoadButtonClicked);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        private void OnLoadButtonClicked()
        {
            var fileName = _view.GetInputField().text;

            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogError("File name is null or empty.");
                return;
            }
            
            LoadButtonClicked?.Invoke(fileName);
        }

        private void OnCloseButtonClicked()
        {
            Closed?.Invoke();
            
            _openButton.gameObject.SetActive(true);
            _view.Hide();
        }

        private void OnShowButtonClicked()
        {
            Opened?.Invoke();
            
            _openButton.gameObject.SetActive(false);
            _view.Show();
            
            EventSystem.current.SetSelectedGameObject(_view.GetInputField().gameObject, null);
            _view.GetInputField().OnPointerClick(new PointerEventData(EventSystem.current));
        }
    }
}