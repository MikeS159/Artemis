﻿using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Artemis.InjectionFactories;
using Artemis.Managers;
using Artemis.Models;
using Artemis.Modules.Effects.ProfilePreview;
using Caliburn.Micro;
using Ninject;

namespace Artemis.ViewModels.Abstract
{
    public abstract class GameViewModel : Screen
    {
        private bool _doActivate;

        private bool _editorShown;
        private GameSettings _gameSettings;
        private EffectModel _lastEffect;

        protected GameViewModel(MainManager mainManager, GameModel gameModel, IEventAggregator events,
            IProfileEditorViewModelFactory pFactory)
        {
            MainManager = mainManager;
            GameModel = gameModel;
            Events = events;
            PFactory = pFactory;
            GameSettings = gameModel.Settings;

            ProfileEditor = PFactory.CreateProfileEditorViewModel(Events, mainManager, gameModel);
            GameModel.Profile = ProfileEditor.SelectedProfile;
            ProfileEditor.PropertyChanged += ProfileUpdater;
        }

        [Inject]
        public ProfilePreviewModel ProfilePreviewModel { get; set; }

        public IEventAggregator Events { get; set; }
        public IProfileEditorViewModelFactory PFactory { get; set; }

        public ProfileEditorViewModel ProfileEditor { get; set; }

        public GameModel GameModel { get; set; }
        public MainManager MainManager { get; set; }

        public GameSettings GameSettings
        {
            get { return _gameSettings; }
            set
            {
                if (Equals(value, _gameSettings)) return;
                _gameSettings = value;
                NotifyOfPropertyChange(() => GameSettings);
            }
        }

        public bool GameEnabled => MainManager.EffectManager.ActiveEffect == GameModel;

        public void ToggleEffect()
        {
            GameModel.Enabled = GameSettings.Enabled;
        }

        public void SaveSettings()
        {
            GameSettings?.Save();
            if (!GameEnabled)
                return;

            // Restart the game if it's currently running to apply settings.
            MainManager.EffectManager.ChangeEffect(GameModel, MainManager.LoopManager);
        }

        public async void ResetSettings()
        {
            var resetConfirm =
                await
                    MainManager.DialogService.ShowQuestionMessageBox("Reset effect settings",
                        "Are you sure you wish to reset this effect's settings? \nAny changes you made will be lost.");

            if (!resetConfirm.Value)
                return;

            GameSettings.ToDefault();
            NotifyOfPropertyChange(() => GameSettings);

            SaveSettings();
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            // OnActive is triggered at odd moments, only activate the profile 
            // preview if OnDeactivate isn't called right after it
            _doActivate = true;
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                if (_doActivate)
                    SetEditorShown(true);
            });
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);

            _doActivate = false;
            SetEditorShown(false);
        }

        public void SetEditorShown(bool enable)
        {
            if (enable == _editorShown)
                return;

            if (enable)
            {
                // Store the current effect so it can be restored later
                if (!(MainManager.EffectManager.ActiveEffect is ProfilePreviewModel))
                    _lastEffect = MainManager.EffectManager.ActiveEffect;

                ProfilePreviewModel.SelectedProfile = ProfileEditor.SelectedProfile;
                MainManager.EffectManager.ChangeEffect(ProfilePreviewModel, MainManager.LoopManager);
            }
            else
            {
                if (_lastEffect != null)
                {
                    // Game models are only used if they are enabled
                    var gameModel = _lastEffect as GameModel;
                    if (gameModel != null)
                        if (!gameModel.Enabled)
                            MainManager.EffectManager.GetLastEffect();
                        else
                            MainManager.EffectManager.ChangeEffect(_lastEffect, MainManager.LoopManager);
                }
                else
                    MainManager.EffectManager.ClearEffect();
            }

            _editorShown = enable;
        }

        private void ProfileUpdater(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "SelectedProfile")
                return;

            GameModel.Profile = ProfileEditor.SelectedProfile;
            ProfilePreviewModel.SelectedProfile = ProfileEditor.SelectedProfile;
        }
    }

    public delegate void OnLayersUpdatedCallback(object sender);
}