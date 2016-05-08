﻿using System;
using System.Collections.Generic;
using System.Linq;
using Artemis.Events;
using Artemis.KeyboardProviders;
using Artemis.Settings;
using Caliburn.Micro;
using NLog;

namespace Artemis.Managers
{
    /// <summary>
    ///     Manages the keyboard providers
    /// </summary>
    public class KeyboardManager
    {
        private readonly IEventAggregator _events;
        private readonly ILogger _logger;
        private KeyboardProvider _activeKeyboard;

        public KeyboardManager(IEventAggregator events, ILogger logger)
        {
            _logger = logger;
            _logger.Info("Intializing KeyboardManager");

            _events = events;
            KeyboardProviders = ProviderHelper.GetKeyboardProviders();

            _logger.Info("Intialized KeyboardManager");
        }

        public List<KeyboardProvider> KeyboardProviders { get; set; }

        public KeyboardProvider ActiveKeyboard
        {
            get { return _activeKeyboard; }
            set
            {
                _activeKeyboard = value;
                // Let the ViewModels know
                _events.PublishOnUIThread(new ActiveKeyboardChanged(value?.Name));
            }
        }

        /// <summary>
        ///     Enables the last keyboard according to the settings file
        /// </summary>
        public void EnableLastKeyboard()
        {
            _logger.Debug($"Enabling last keyboard: {General.Default.LastKeyboard}");
            if (string.IsNullOrEmpty(General.Default.LastKeyboard))
                return;

            var keyboard = KeyboardProviders.FirstOrDefault(k => k.Name == General.Default.LastKeyboard);
            EnableKeyboard(keyboard);
        }

        /// <summary>
        ///     Enables the given keyboard
        /// </summary>
        /// <param name="keyboardProvider"></param>
        public void EnableKeyboard(KeyboardProvider keyboardProvider)
        {
            if (keyboardProvider == null)
                throw new ArgumentNullException(nameof(keyboardProvider));

            if (ActiveKeyboard?.Name == keyboardProvider.Name)
                return;

            var wasNull = false;
            if (ActiveKeyboard == null)
            {
                wasNull = true;
                ActiveKeyboard = keyboardProvider;
            }

            lock (ActiveKeyboard)
            {
                _logger.Debug($"Enabling keyboard: {keyboardProvider.Name}");

                if (!wasNull)
                    ReleaseActiveKeyboard();

                // Disable everything if there's no active keyboard found
                if (!keyboardProvider.CanEnable())
                {
                    // TODO: MainManager.DialogService.ShowErrorMessageBox(keyboardProvider.CantEnableText);
                    General.Default.LastKeyboard = null;
                    General.Default.Save();
                    return;
                }

                ActiveKeyboard = keyboardProvider;
                ActiveKeyboard.Enable();

                General.Default.LastKeyboard = ActiveKeyboard.Name;
                General.Default.Save();
            }
        }

        /// <summary>
        ///     Releases the active keyboard, if CanDisable is true
        /// </summary>
        public void ReleaseActiveKeyboard()
        {
            lock (ActiveKeyboard)
            {
                if (ActiveKeyboard == null)
                    return;

                ActiveKeyboard.Disable();
                ActiveKeyboard = null;
            }

            _logger.Debug($"Released keyboard: {ActiveKeyboard?.Name}");
        }
    }
}