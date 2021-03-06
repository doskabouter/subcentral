﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Text.RegularExpressions;
using MediaPortal.GUI.Library;
using NLog;
using SubCentral.GUI.Items;
using SubCentral.Localizations;
using SubCentral.PluginHandlers;
using SubCentral.Settings;
using SubCentral.Settings.Data;
using SubCentral.Utils;

namespace SubCentral.GUI {
    public class SubCentralGUI : GUIWindow {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private SubCentralCore core = SubCentralCore.Instance;
        private Retriever retriever = Retriever.Instance;

        private static WaitCursor waitCursor;

        public const int SubCentralGUIID = 84623;
        public readonly List<int> MainWindowIDs = new List<int> { 0, 34, 35, SubCentralGUIID };

        #region Private Variables
        private int _lastSelectedGroupsAndProvidersItemIndex = 0;
        private int _lastSelectedSubtitlesItemIndex = 1;
        private bool _GUIInitialized = false;
        private BasicMediaDetail _modifySearchMediaDetail = new BasicMediaDetail();
        private bool _backupHandlerSet = false;
        private bool _notificationDone = false;
        private SubtitlesSortMethod _subtitlesSortMethod = SubtitlesSortMethod.DefaultNoSort;
        private bool _subtitlesSortAsc = true;
        private bool _clearedMedia = false;
        private bool _shouldDeleteButtonVisible = false;
        private List<FileInfo> _subtitleFilesForCurrentMedia = new List<FileInfo>();
        private bool _subtitlesExistForCurrentMedia = false;
        private bool _checkMediaForSubtitlesOnOpenDone = false;
        private bool _mediaAvailable = false;
        #endregion

        #region GUI Controls
        //[SkinControl((int)GUIControls.SEARCHBUTTON)]
        //protected GUIButtonControl searchButton = null;
        [SkinControl((int)GUIControls.CANCELBUTTON)]
        protected GUIButtonControl cancelButton = null;
        [SkinControl((int)GUIControls.LANGUAGESBUTTON)]
        protected GUIButtonControl languagesButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHBUTTON)]
        protected GUIButtonControl modifySearchButton = null;
        [SkinControl((int)GUIControls.SORTBUTTON)]
        protected GUISortButtonControl sortButton = null;
        [SkinControl((int)GUIControls.DELETEBUTTON)]
        protected GUIButtonControl deleteButton = null;

        [SkinControl((int)GUIControls.PROVIDERSLIST)]
        protected GUIListControl providerList = null;

        [SkinControl((int)GUIControls.MODIFYSEARCHOKBUTTON)]
        protected GUIButtonControl modifySearchOKButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHCANCELBUTTON)]
        protected GUIButtonControl modifySearchCancelButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHREVERTBUTTON)]
        protected GUIButtonControl modifySearchRevertButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHCLEARFILESBUTTON)]
        protected GUIButtonControl modifySearchClearFilesButton = null;
        //[SkinControl((int)GUIControls.MODIFYSEARCHSELECTFOLDERBUTTON)]
        //protected GUIButtonControl modifySearchSelectFolderButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHMOVIEBUTTON)]
        protected GUIButtonControl modifySearchMovieButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHTVSHOWBUTTON)]
        protected GUIButtonControl modifySearchTVShowButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHIMDBIDBUTTON)]
        protected GUIButtonControl modifySearchIMDbIDButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHTITLEBUTTON)]
        protected GUIButtonControl modifySearchTitleButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHYEARBUTTON)]
        protected GUIButtonControl modifySearchYearButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHSEASONBUTTON)]
        protected GUIButtonControl modifySearchSeasonButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHEPISODEBUTTON)]
        protected GUIButtonControl modifySearchEpisodeButton = null;
        [SkinControl((int)GUIControls.MODIFYSEARCHABSOLUTENUMBERINGBUTTON)]
        protected GUICheckMarkControl modifySearchAbsoluteNumberingButton = null;
        #endregion

        #region Dummy GUI Controls
        [SkinControl((int)GUIControls.DUMMYMAINVIEW)]
        protected GUILabelControl mainViewIndicator = null;
        [SkinControl((int)GUIControls.DUMMYSEARCHVIEW)]
        protected GUILabelControl searchViewIndicator = null;
        [SkinControl((int)GUIControls.DUMMYMODIFYSEARCH)]
        protected GUILabelControl modifySearchViewIndicator = null;
        #endregion

        #region Properties
        public ViewMode View {
            get {
                return _viewMode;
            }
            set {
                _viewMode = value;

                if (mainViewIndicator != null) mainViewIndicator.Visible = (_viewMode == ViewMode.MAIN);
                if (searchViewIndicator != null) searchViewIndicator.Visible = (_viewMode == ViewMode.SEARCH);
                if (modifySearchViewIndicator != null) modifySearchViewIndicator.Visible = (_viewMode == ViewMode.MODIFYSEARCH);

                if (_GUIInitialized)
                    GUIWindowManager.Process();

                switch (_viewMode) {
                    case ViewMode.NONE:
                        GUIUtils.SetProperty("#SubCentral.Header.Label", SubCentralUtils.PluginName());
                        GUIUtils.SetProperty("#SubCentral.Header.ShortLabel", string.Empty);
                        break;
                    case ViewMode.MAIN:
                        GUIUtils.SetProperty("#SubCentral.Header.Label", string.Format("{0} - {1}", SubCentralUtils.PluginName(), Localization.About));
                        GUIUtils.SetProperty("#SubCentral.Header.ShortLabel", Localization.About);
                        GUIControl.FocusControl(GetID, _defaultControlId);
                        break;
                    case ViewMode.SEARCH:
                        GUIUtils.SetProperty("#SubCentral.Header.Label", string.Format("{0} - {1}", SubCentralUtils.PluginName(), Localization.SubtitleSearch));
                        GUIUtils.SetProperty("#SubCentral.Header.ShortLabel", Localization.SubtitleSearch);
                        GUIControl.FocusControl(GetID, (int)GUIControls.PROVIDERSLIST);
                        break;
                    case ViewMode.MODIFYSEARCH:
                        _modifySearchMediaDetail = CopyMediaDetail(CurrentHandler.MediaDetail);
                        modifySearchClearFilesButton.Visible = _modifySearchMediaDetail.Files != null && _modifySearchMediaDetail.Files.Count > 0;
                        modifySearchAbsoluteNumberingButton.Selected = _modifySearchMediaDetail.AbsoluteNumbering;
                        if (!_modifySearchMediaDetail.AbsoluteNumbering)
                          GUIControl.EnableControl(GetID, modifySearchSeasonButton.GetID);
                        else
                          GUIControl.DisableControl(GetID, modifySearchSeasonButton.GetID);
                        _clearedMedia = false;
                        //modifySearchSelectFolderButton.Visible = CurrentHandler.MediaDetail.Files == null || _modifySearchMediaDetail.Files.Count == 0;
                        PublishSearchProperties(true);
                        if (_backupHandler == null) {
                            GUIUtils.SetProperty("#SubCentral.Header.Label", string.Format("{0} - {1}", SubCentralUtils.PluginName(), Localization.ManualSearch));
                            GUIUtils.SetProperty("#SubCentral.Header.ShortLabel", Localization.ManualSearch);
                        }
                        else {
                            GUIUtils.SetProperty("#SubCentral.Header.Label", string.Format("{0} - {1}", SubCentralUtils.PluginName(), Localization.ModifySearch));
                            GUIUtils.SetProperty("#SubCentral.Header.ShortLabel", Localization.ModifySearch);
                        }
                        //GUIControl.FocusControl(GetID, (int)GUIControls.MODIFYSEARCHOKBUTTON);
                        GUIControl.FocusControl(GetID, (int)GUIControls.MODIFYSEARCHTITLEBUTTON);
                        break;
                }
                if (_GUIInitialized) {
                    UpdateButtonStates();
                    PublishSearchProperties();
                }
            }
        }
        private ViewMode _viewMode;

        public ListControlViewState ListControlViewState {
            get {
                return _listControlViewState;
            }
            set {
                _listControlViewState = value;
                UpdateSortButton();
            }
        }
        private ListControlViewState _listControlViewState;

        private PluginHandler CurrentHandler {
            get {
                return _currentHandler;
            }
            set {
                //if (_currentHandler == null)
                if (!_backupHandlerSet) {
                    _backupHandler = value;
                    _backupHandlerSet = true;
                }

                _currentHandler = value;

                if (_currentHandler == null)
                    modifySearchButton.Label = Localization.ManualSearch;
                else
                    modifySearchButton.Label = Localization.ModifySearch;
            }
        }
        private PluginHandler _currentHandler = null;
        private PluginHandler _backupHandler = null;

        public SubtitlesSearchType ModifySearchSearchType {
            get {
                return _modifySearchSearchType;
            }
            set {
                if (value == SubtitlesSearchType.IMDb) value = SubtitlesSearchType.MOVIE;

                _modifySearchSearchType = value;

                GUIUtils.SetProperty("#SubCentral.ModifySearch.SearchType.Type", value.ToString());
            }
        }
        private SubtitlesSearchType _modifySearchSearchType;
        private SubtitlesSearchType _oldModifySearchSearchType;
        #endregion

        #region GUIWindow Methods
        public override int GetID {
            get {
                return SubCentralGUIID;
            }
        }

        public override bool Init() {
            base.Init();
            core.Initialize();
            logger.Info("Initializing GUI");

            retriever.OnSubtitleSearchCompletedEvent += new Retriever.OnSubtitleSearchCompletedDelegate(retriever_OnSubtitleSearchCompletedEvent);
            retriever.OnSubtitlesSearchErrorEvent += new Retriever.OnSubtitlesSearchErrorDelegate(retriever_OnSubtitlesSearchErrorEvent);
            retriever.OnSubtitleDownloadedToTempEvent += new Retriever.OnSubtitleDownloadedToTempDelegate(retriever_OnSubtitleDownloadedToTempEvent);
            retriever.OnSubtitleDownloadedEvent += new Retriever.OnSubtitleDownloadedDelegate(retriever_OnSubtitleDownloadedEvent);
            retriever.OnSubtitlesDownloadErrorEvent += new Retriever.OnSubtitlesDownloadErrorDelegate(retriever_OnSubtitlesDownloadErrorEvent);

            GUIUtils.SetProperty("#SubCentral.About", Localization.AboutText);

            // check if we can load the skin
            bool success = Load(GUIGraphicsContext.Skin + @"\SubCentral.xml");

            return success;
        }

        public override void DeInit() {
            base.DeInit();
            SettingsManager.Save();
        }

        protected override void OnPageLoad() {
            _GUIInitialized = false;
            _currentHandler = null;
            _backupHandler = null;
            _backupHandlerSet = false;
            _shouldDeleteButtonVisible = false;
            _clearedMedia = false;
            _subtitleFilesForCurrentMedia.Clear();
            _subtitlesExistForCurrentMedia = false;
            _mediaAvailable = false;
            _checkMediaForSubtitlesOnOpenDone = false;
            View = ViewMode.NONE;
            ModifySearchSearchType = SubtitlesSearchType.NONE;
            _oldModifySearchSearchType = SubtitlesSearchType.NONE;
            ListControlViewState = ListControlViewState.GROUPSANDPROVIDERS;

            _subtitlesSortMethod = Settings.SettingsManager.Properties.GUISettings.SortMethod;
            _subtitlesSortAsc = Settings.SettingsManager.Properties.GUISettings.SortAscending;

            GUIUtils.SetProperty("#currentmodule", SubCentralUtils.PluginName());

            //if (searchButton == null) {
            if (!CheckAndTranslateSkin()) {
                // if the component didn't load properly we probably have a bad skin file
                GUIUtils.ShowOKDialog(Localization.Error, Localization.CannotLoadSkin);
                GUIWindowManager.ShowPreviousWindow();
                logger.Error("Failed to load all components from skin file. Skin is outdated or invalid.");
                return;
            }
            // test SubtitleDownloader assembly
            if (!core.SubtitleDownloaderInitialized) {
                GUIUtils.ShowOKDialog(Localization.Error, string.Concat(Localization.UnableToLoadSubtitleDownloader, "\n", Localization.SubtitleDownloaderUnavailable));
                GUIWindowManager.ShowPreviousWindow();
                logger.Error("SubtitleDownloader: not available");
                return;
            }

            base.OnPageLoad();
            _GUIInitialized = true;

            GUIControl.DisableControl(GetID, deleteButton.GetID);

            if (sortButton != null)
                sortButton.SortChanged += new SortEventHandler(OnSortChanged);

            // initialize properties
            InitSearchProperties();

            // initialize list control
            _lastSelectedGroupsAndProvidersItemIndex = 0;
            _lastSelectedSubtitlesItemIndex = 1;
            FillProviderGroupsAndProviders(false);

            // get last active module  
            int lastActiveWindow = GUIWindowManager.GetPreviousActiveWindow();

            // just entering from the home or basic home or plugins screen, show the main screen
            if (MainWindowIDs.Contains(lastActiveWindow)) {
                logger.Debug("Entered plugin from Home or Basic Home or Plugins screen");
                View = ViewMode.MAIN;
                CurrentHandler = null;
            }

            // entering from a plugin
            else {
                //try to load data from it and if we fail return back to the calling plugin.
                if (!LoadFromPlugin(lastActiveWindow)) {
                    logger.Error("Entered plugin from ID #{0}, but failed to load an appropriate handler.", lastActiveWindow);

                    // show a popup notifying the user
                    if (!GUIUtils.ShowCustomYesNoDialog(Localization.Warning, string.Format(Localization.UnableToImportDataFrom, CurrentHandler == null ? Localization.ExternalPlugin : CurrentHandler.PluginName), Localization.ManualSearch, Localization.Back)) {
                        GUIWindowManager.ShowPreviousWindow();
                        return;
                    }
                    //View = ViewMode.MAIN;
                    OnModifySearch();
                }

                // we loaded details successfully, we are now in file view.
                else {
                    logger.Debug("Entered plugin from {0} ({1})", CurrentHandler.PluginName, CurrentHandler.ID);
                    SubtitlesSearchType searchType = SubCentralUtils.getSubtitlesSearchTypeFromMediaDetail(CurrentHandler.MediaDetail);
                    View = ViewMode.SEARCH;

                    //if (searchType != SubtitlesSearchType.NONE)
                    {
                        EnableDisableProviderGroupsAndProviders(searchType != SubtitlesSearchType.NONE);

                        if (CurrentHandler.MediaDetail.Files != null && CurrentHandler.MediaDetail.Files.Count > 0) {
                          _shouldDeleteButtonVisible = true;
                          GUIControl.EnableControl(GetID, deleteButton.GetID);
                        }

                        if (Settings.SettingsManager.Properties.GUISettings.CheckMediaForSubtitlesOnOpen) {
                            CheckMediaForSubtitlesOnOpen(CurrentHandler);
                        }

                        if (Settings.SettingsManager.Properties.GeneralSettings.PluginLoadWithSearchData == OnPluginLoadWithSearchData.SearchDefaults) {
                            SettingsGroup defaultGroup = SubCentralUtils.getDefaultGroupForSearchType(searchType);
                            if (defaultGroup != null) {
                                _lastSelectedGroupsAndProvidersItemIndex = LocateGroupInListControl(defaultGroup);
                                PerformSearch(defaultGroup);
                            }
                        }
                    }
                }
            }
        }

        protected override void OnPageDestroy(int new_windowId) {
            base.OnPageDestroy(new_windowId);
            RetrieverAbort();
            core.PluginHandlers.ClearCustomHandlers();

            Settings.SettingsManager.Properties.GUISettings.SortMethod = _subtitlesSortMethod;
            Settings.SettingsManager.Properties.GUISettings.SortAscending = _subtitlesSortAsc;

            if (sortButton != null)
                sortButton.SortChanged -= OnSortChanged;
            //if (CurrentHandler != null)
            //    CurrentHandler.Clear();
        }

        protected override void OnClicked(int controlId, GUIControl control, MediaPortal.GUI.Library.Action.ActionType actionType) {
            base.OnClicked(controlId, control, actionType);

            switch (controlId) {
                case (int)GUIControls.SEARCHBUTTON:
                    //PerformSearch();
                    break;

                case (int)GUIControls.CANCELBUTTON:
                    GUIWindowManager.ShowPreviousWindow();
                    break;

                case (int)GUIControls.LANGUAGESBUTTON:
                    OnLanguageSelection();
                    break;

                case (int)GUIControls.MODIFYSEARCHBUTTON:
                    OnModifySearch();
                    break;

                case (int)GUIControls.SORTBUTTON:
                    OnShowDialogSortOptions();
                    break;

                case (int)GUIControls.DELETEBUTTON:
                    OnDeleteSubtitles();
                    break;

                case (int)GUIControls.PROVIDERSLIST:
                    // have to check action type for facade
                    if (actionType == MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM) {
                        OnListSelection();
                    }
                    break;

                case (int)GUIControls.MODIFYSEARCHOKBUTTON:
                    ModifySearchWithUserData();
                    break;

                case (int)GUIControls.MODIFYSEARCHCANCELBUTTON:
                    CancelModifySearch();
                    break;

                case (int)GUIControls.MODIFYSEARCHREVERTBUTTON:
                    RevertModifySearch(true);
                    break;

                case (int)GUIControls.MODIFYSEARCHCLEARFILESBUTTON:
                    ClearFiles();
                    break;

                case (int)GUIControls.MODIFYSEARCHMOVIEBUTTON:
                    if (_oldModifySearchSearchType == SubtitlesSearchType.NONE)
                        _oldModifySearchSearchType = ModifySearchSearchType;
                    ModifySearchSearchType = SubtitlesSearchType.MOVIE;
                    break;

                case (int)GUIControls.MODIFYSEARCHTVSHOWBUTTON:
                    if (_oldModifySearchSearchType == SubtitlesSearchType.NONE)
                        _oldModifySearchSearchType = ModifySearchSearchType;
                    ModifySearchSearchType = SubtitlesSearchType.TVSHOW;
                    break;

                case (int)GUIControls.MODIFYSEARCHIMDBIDBUTTON:
                case (int)GUIControls.MODIFYSEARCHTITLEBUTTON:
                case (int)GUIControls.MODIFYSEARCHYEARBUTTON:
                case (int)GUIControls.MODIFYSEARCHSEASONBUTTON:
                case (int)GUIControls.MODIFYSEARCHEPISODEBUTTON:
                    KeyboardModifySearch(controlId);
                    PublishSearchProperties(true);
                    break;
                
                case (int)GUIControls.MODIFYSEARCHABSOLUTENUMBERINGBUTTON:
                    ModifyAbsoluteNumbering();
                    break;
            }
        }

        public override void OnAction(MediaPortal.GUI.Library.Action action) {
            switch (action.wID) {
                //case MediaPortal.GUI.Library.Action.ActionType.ACTION_PARENT_DIR:
                //case MediaPortal.GUI.Library.Action.ActionType.ACTION_HOME:
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_PREVIOUS_MENU:
                    if (View == ViewMode.MODIFYSEARCH)
                        CancelModifySearch();
                    else if (GetFocusControlId() == (int)GUIControls.PROVIDERSLIST && RetrieverRunning()) {
                        RetrieverAbort();
                    }
                    else if (GetFocusControlId() == (int)GUIControls.PROVIDERSLIST && ListControlViewState != ListControlViewState.GROUPSANDPROVIDERS)
                        OnListBack();
                    else
                        GUIWindowManager.ShowPreviousWindow();
                    break;
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_CONTEXT_MENU:
                    GUIListItem selectedItem = GUIControl.GetSelectedListItem(GetID, (int)GUIControls.PROVIDERSLIST);

                    if (GetFocusControlId() == (int)GUIControls.PROVIDERSLIST && selectedItem != null && !selectedItem.IsRemote && selectedItem.MusicTag != null) {
                        if (ListControlViewState == ListControlViewState.GROUPSANDPROVIDERS) {
                            List<GUIListItem> contextMenuItems = new List<GUIListItem>();
                            contextMenuItems.Add(new GUIListItem(Localization.Search));
                            switch (GUIUtils.ShowMenuDialog(Localization.ContextMenu, contextMenuItems)) {
                                case 0:
                                    OnListSelection();
                                    break;
                            }
                        }
                        else if (ListControlViewState == ListControlViewState.SEARCHRESULTS) {
                            List<GUIListItem> contextMenuItems = new List<GUIListItem>();
                            contextMenuItems.Add(new GUIListItem(Localization.Download));
                            contextMenuItems.Add(new GUIListItem(Localization.DownloadTo));
                            switch (GUIUtils.ShowMenuDialog(Localization.ContextMenu, contextMenuItems)) {
                                case 0:
                                    OnListSelection();
                                    break;
                                case 1:
                                    _lastSelectedSubtitlesItemIndex = providerList.SelectedListItemIndex;
                                    PerformDownload((SubtitleItem)selectedItem.MusicTag, true);
                                    break;
                            }
                        }
                    }
                    else {
                        base.OnAction(action);
                    }
                    break;
                default:
                    base.OnAction(action);
                    break;
            }
        }

        public override bool OnMessage(GUIMessage message) {
            return base.OnMessage(message);
        }
        #endregion

        #region Private Methods
        private bool LoadFromPlugin(int pluginID) {
            bool success = false;

            // try to grab our handler
            CurrentHandler = core.PluginHandlers[pluginID];
            if (CurrentHandler != null)
                success = CurrentHandler.Update();

            return success;
        }

        private void PerformSearch(object tag) {
            if (tag == null) return;

            if (CurrentHandler == null) return;

            if (RetrieverRunning()) {
                if (RetrieverCanceled())
                    RetrieverAbort();
                else
                    return;
            }

            _notificationDone = false;
            retriever.OnProviderSearchErrorEvent -= retriever_OnProviderSearchErrorEvent;

            try {
                if (tag is SettingsGroup) {
                    SettingsGroup settingsGroup = tag as SettingsGroup;

                    BasicMediaDetail details = CurrentHandler.MediaDetail;

                    retriever.FillData(true,
                                       SubCentralUtils.getSelectedLanguageCodes(),
                                       SubCentralUtils.getProviderIDsAndTitles(SubCentralUtils.getEnabledProvidersFromGroup(settingsGroup)),
                                       getRealCurrentSearchSearchType(details)
                                       );

                    retriever.SearchForSubtitles(details);
                }
                else if (tag is SettingsProvider) {
                    //ShowWaitCursor();

                    SettingsProvider settingsProvider = tag as SettingsProvider;

                    BasicMediaDetail details = CurrentHandler.MediaDetail;

                    retriever.FillData(false,
                                       SubCentralUtils.getSelectedLanguageCodes(),
                                       SubCentralUtils.getProviderIDsAndTitles(new List<SettingsProvider> { settingsProvider }),
                                       getRealCurrentSearchSearchType(details)
                                       );

                    retriever.OnProviderSearchErrorEvent += new Retriever.OnProviderSearchErrorDelegate(retriever_OnProviderSearchErrorEvent);

                    retriever.SearchForSubtitles(details);

                }
            }
            catch (Exception e) {
                //HideWaitCursor();
                logger.ErrorException(string.Format("Error while retrieving subtitles{0}", Environment.NewLine), e);
                GUIUtils.ShowNotifyDialog(Localization.Error, Localization.ErrorWhileRetrievingSubtitles, GUIUtils.NoSubtitlesLogoThumbPath);
            }
        }

        void retriever_OnProviderSearchErrorEvent(BasicMediaDetail mediaDetail, SubtitlesSearchType subtitlesSearchType, Exception e) {
            retriever.OnProviderSearchErrorEvent -= retriever_OnProviderSearchErrorEvent;
            //HideWaitCursor();
            if (e is NotImplementedException || e is NotSupportedException) {
                switch (subtitlesSearchType) {
                    case SubtitlesSearchType.IMDb:
                        GUIUtils.ShowNotifyDialog(Localization.Error, Localization.SiteDoesNotSupportIMDbIDSearch, GUIUtils.NoSubtitlesLogoThumbPath);
                        break;
                    case SubtitlesSearchType.MOVIE:
                        GUIUtils.ShowNotifyDialog(Localization.Error, Localization.SiteDoesNotSupportMovieSearch, GUIUtils.NoSubtitlesLogoThumbPath);
                        break;
                    case SubtitlesSearchType.TVSHOW:
                        GUIUtils.ShowNotifyDialog(Localization.Error, Localization.SiteDoesNotSupportTVShowSearch, GUIUtils.NoSubtitlesLogoThumbPath);
                        break;
                    default:
                        GUIUtils.ShowNotifyDialog(Localization.Error, string.Format(Localization.ErrorWhileRetrievingSubtitlesWithReason, e.Message), GUIUtils.NoSubtitlesLogoThumbPath, 10);
                        //GUIUtils.ShowNotifyDialog(Localization.Error, Localization.ErrorWhileRetrievingSubtitles, GUIUtils.NoSubtitlesLogoThumbPath);
                        break;
                }
            }
            else if (e is System.Net.WebException && ((System.Net.WebException)e).Status == System.Net.WebExceptionStatus.Timeout) {
                GUIUtils.ShowNotifyDialog(Localization.Error, Localization.TimedOutWhileRetrievingSubtitles, GUIUtils.NoSubtitlesLogoThumbPath);
            }
            else {
                GUIUtils.ShowNotifyDialog(Localization.Error, string.Format(Localization.ErrorWhileRetrievingSubtitlesWithReason, e.Message), GUIUtils.NoSubtitlesLogoThumbPath, 10);
                //GUIUtils.ShowNotifyDialog(Localization.Error, Localization.ErrorWhileRetrievingSubtitles, GUIUtils.NoSubtitlesLogoThumbPath);
            }
            _notificationDone = true;
        }

        void retriever_OnSubtitlesSearchErrorEvent(Exception e) {
            retriever.OnProviderSearchErrorEvent -= retriever_OnProviderSearchErrorEvent;
            //HideWaitCursor();
            logger.ErrorException(string.Format("Error while retrieving subtitles{0}", Environment.NewLine), e);
            GUIUtils.ShowNotifyDialog(Localization.Error, string.Format(Localization.ErrorWhileRetrievingSubtitlesWithReason, e.Message), GUIUtils.NoSubtitlesLogoThumbPath, 10);
            //GUIUtils.ShowNotifyDialog(Localization.Error, Localization.ErrorWhileRetrievingSubtitles, GUIUtils.NoSubtitlesLogoThumbPath);
            _notificationDone = false;
        }

        void retriever_OnSubtitlesDownloadErrorEvent(Exception e) {
            //HideWaitCursor();
            logger.ErrorException(string.Format("Error while downloading subtitles{0}", Environment.NewLine), e);
            GUIUtils.ShowNotifyDialog(Localization.Error, string.Format(Localization.ErrorWhileDownloadingSubtitlesWithReason, e.Message), GUIUtils.NoSubtitlesLogoThumbPath, 10);
            //GUIUtils.ShowNotifyDialog(Localization.Error, Localization.ErrorWhileDownloadingSubtitles, GUIUtils.NoSubtitlesLogoThumbPath);
            _notificationDone = false;
        }

        private bool RetrieverRunning() {
            if (retriever == null) return false;
            else return retriever.IsRunning();
        }

        private bool RetrieverCanceled() {
            if (retriever == null) return false;
            else return retriever.IsCanceled();
        }

        private void RetrieverAbort() {
            if (RetrieverRunning()) {
                retriever.Kill();
            }
            //HideWaitCursor();
        }

        void retriever_OnSubtitleSearchCompletedEvent(List<SubtitleItem> subtitleItems, bool isCanceled) {
            retriever.OnProviderSearchErrorEvent -= retriever_OnProviderSearchErrorEvent;
            //HideWaitCursor();
            bool bCanceled = isCanceled;
            if (bCanceled && Settings.SettingsManager.Properties.GeneralSettings.ShowResultsAfterProgressCancel && subtitleItems != null && subtitleItems.Count > 0) {
                bCanceled = false;
            }
            if (!bCanceled && !_notificationDone) {
                FillSubtitleSearchResults(subtitleItems);
            }
            _notificationDone = false;
        }

        private void PerformDownload(SubtitleItem subtitleItem) {
            PerformDownload(subtitleItem, false);
        }

        private void PerformDownload(SubtitleItem subtitleItem, bool skipDefaults) {
            if (CurrentHandler == null) return;

            FileInfo fileInfo = null;
            if (CurrentHandler.MediaDetail.Files != null && CurrentHandler.MediaDetail.Files.Count > 0)
                fileInfo = CurrentHandler.MediaDetail.Files[0];

            List<FolderSelectionItem> items = SubCentralUtils.getEnabledAndValidFoldersForMedia(fileInfo, true);
            if (items == null || items.Count == 0) {
                GUIUtils.ShowNotifyDialog(Localization.Warning, Localization.NoDownloadFolders);
                return;
            }

            SubtitlesSearchType searchType = getRealCurrentSearchSearchType(CurrentHandler.MediaDetail);

            bool readOnly = true;
            bool create = false;
            int selectedFolderIndex = -1;
            bool selectedFromDefault = false;

            while (readOnly || !create) {
                selectedFolderIndex = -1;

                if (!selectedFromDefault && !skipDefaults && Settings.SettingsManager.Properties.FolderSettings.OnDownload == OnDownload.DefaultFolders) {
                    for (int i = 0; i < items.Count; i++) {
                        FolderSelectionItem folderSelectionItem = items[i];
                        if ((searchType == SubtitlesSearchType.IMDb || searchType == SubtitlesSearchType.MOVIE) && folderSelectionItem.DefaultForMovies) {
                            selectedFolderIndex = i;
                            selectedFromDefault = true;
                            break;
                        }
                        else if (searchType == SubtitlesSearchType.TVSHOW && folderSelectionItem.DefaultForTVShows) {
                            selectedFolderIndex = i;
                            selectedFromDefault = true;
                            break;
                        }
                    }
                }

                if (selectedFolderIndex < 0) {
                    if (!skipDefaults && Settings.SettingsManager.Properties.FolderSettings.OnDownload == OnDownload.DefaultFolders) {
                        logger.Info("Default folder for media type {0} could not be found, manually input folder for download", searchType.ToString());
                    }
                    selectedFolderIndex = SubCentralUtils.ShowFolderMenuDialog(Localization.SelectDownloadFolder, items, selectedFolderIndex);
                }

                if (selectedFolderIndex >= 0) {
                    readOnly = false;
                    create = true;

                    string folderName = items[selectedFolderIndex].FolderName;

                    if (items[selectedFolderIndex].FolderErrorInfo == FolderErrorInfo.ReadOnly) {
                        readOnly = true;
                        if (selectedFromDefault) {
                            logger.Info("Default folder {0} for media type {1} is not writable, manually input folder for download", folderName, searchType.ToString());
                            GUIUtils.ShowNotifyDialog(Localization.Warning, string.Format(Localization.DefaultFolderNotWritable, folderName));
                        }
                    }
                    else if (items[selectedFolderIndex].FolderErrorInfo == FolderErrorInfo.Inaccessible) {
                        readOnly = true;
                        if (selectedFromDefault) {
                            logger.Info("Default folder {0} for media type {1} is inaccessible, manually input folder for download", folderName, searchType.ToString());
                            GUIUtils.ShowNotifyDialog(Localization.Warning, string.Format(Localization.DefaultFolderInaccessible, folderName));
                        }
                    }
                    else if (items[selectedFolderIndex].FolderErrorInfo == FolderErrorInfo.NonExistant) {
                        bool dlgResult = false;
                        if (selectedFromDefault) {
                            logger.Info("Default folder {0} for media type {1} does not exist", folderName, searchType.ToString());
                            dlgResult = GUIUtils.ShowYesNoDialog(Localization.Confirm, string.Format(Localization.DefaultDownloadFoderDoesNotExistCreate, folderName), true);
                        }
                        else {
                            dlgResult = GUIUtils.ShowYesNoDialog(Localization.Confirm, Localization.DownloadFoderDoesNotExistCreate, true);
                        }

                        if (dlgResult) {
                            try {
                                Directory.CreateDirectory(folderName);
                            }
                            catch (Exception e) {
                                logger.ErrorException(string.Format("Error creating directory {0}{1}", folderName, Environment.NewLine), e);
                                create = false;
                                GUIUtils.ShowOKDialog(Localization.Error, Localization.ErrorWhileCreatingDirectory);
                            }
                        }
                        else {
                            create = false;
                        }
                    }
                }
                else {
                    break;
                }
            }

            if (selectedFolderIndex >= 0) {
              retriever.FillData(false,
                                 null,
                                 null,
                                 SubtitlesSearchType.NONE
                                 );

              retriever.DownloadSubtitle(subtitleItem, CurrentHandler.MediaDetail, items[selectedFolderIndex], searchType, skipDefaults);
            }
        }

        void retriever_OnSubtitleDownloadedToTempEvent(BasicMediaDetail mediaDetail, List<FileInfo> subtitleFiles) {
            //HideWaitCursor();
        }

        private void retriever_OnSubtitleDownloadedEvent(BasicMediaDetail mediaDetail, List<SubtitleDownloadStatus> statusList) {
            //HideWaitCursor();

            string heading = string.Empty;

            switch (getRealCurrentSearchSearchType(mediaDetail)) {
                case SubtitlesSearchType.TVSHOW:
                    heading = string.Format("{0} S{1}E{2}", mediaDetail.Title, mediaDetail.SeasonStr, mediaDetail.EpisodeStr);
                    break;
                case SubtitlesSearchType.MOVIE:
                    heading = string.Format("{0} ({1})", mediaDetail.Title, mediaDetail.YearStr);
                    break;
                case SubtitlesSearchType.IMDb:
                    heading = string.IsNullOrEmpty(mediaDetail.Title) ? mediaDetail.ImdbIDStr : string.Format("{0} ({1})", mediaDetail.Title, mediaDetail.ImdbIDStr);
                    break;
            }

            if (statusList == null || statusList.Count < 1) {
                if (statusList != null) {
                    GUIUtils.ShowNotifyDialog(heading, string.Format(Localization.NoSubtitlesDownloaded, Localization.NoSubtitlesInChosen), GUIUtils.NoSubtitlesLogoThumbPath);
                }
                return;
            }

            int mediaCount = statusList.Count;
            int succesful, canceled, errors;

            countDownloads(statusList, out succesful, out canceled, out errors);
            if (succesful == mediaCount) { // all succesful
                if (succesful == 1)
                    GUIUtils.ShowNotifyDialog(heading, Localization.SubtitlesDownloaded, GUIUtils.SubtitlesLogoThumbPath);
                else
                    GUIUtils.ShowNotifyDialog(heading, string.Format(Localization.AllSubtitlesDownloaded, succesful), GUIUtils.SubtitlesLogoThumbPath);
            }
            else if (errors == mediaCount) { // all errors
                GUIUtils.ShowNotifyDialog(heading, Localization.ErrorWhileDownloadingSubtitles, GUIUtils.NoSubtitlesLogoThumbPath);
                return;
            }
            else if (canceled == mediaCount) { // all canceled
                /*
                if (canceled == 1)
                    GUIUtils.ShowNotifyDialog(heading, Localization.CanceledDownload, GUIUtils.NoSubtitlesLogoThumbPath);
                else
                    GUIUtils.ShowNotifyDialog(heading, string.Format(Localization.AllSubtitlesCanceledDownload, canceled), GUIUtils.NoSubtitlesLogoThumbPath);
                */
                return;
            }
            else { // some are ok, some not
                List<string> notifyList = new List<string>();
                if (succesful > 0)
                    notifyList.Add(string.Format(Localization.SubtitlesDownloadedCount, succesful));
                if (errors > 0)
                    notifyList.Add(string.Format(Localization.SubtitlesDownloadErrorCount, errors));
                if (canceled > 0)
                    notifyList.Add(string.Format(Localization.SubtitlesDownloadCanceledCount, canceled));

                string notifyListString = string.Empty;
                foreach (string notify in notifyList) {
                    notifyListString = notifyListString + (string.IsNullOrEmpty(notifyListString) ? notify : "\n" + notify);
                }

                if (!string.IsNullOrEmpty(notifyListString))
                    GUIUtils.ShowNotifyDialog(heading, notifyListString);
            }

            // checking
            if (statusList.Count == 0) return;
            if (mediaDetail.Files == null || mediaDetail.Files.Count == 0) return;
            PluginHandler properHandler = _backupHandler != null ? _backupHandler : CurrentHandler;
            if (properHandler == null) return;

            foreach (SubtitleDownloadStatus sds in statusList) {
                if (sds.Index >= 0 && mediaDetail.Files.Count > sds.Index && 
                      (sds.Status == SubtitleDownloadStatusStatus.Succesful || sds.Status == SubtitleDownloadStatusStatus.AlreadyExists)
                   ) {
                    properHandler.SetHasSubtitles(mediaDetail.Files[sds.Index].FullName, true);
                    _checkMediaForSubtitlesOnOpenDone = false;
                }
            }
                
            if (SettingsManager.Properties.GeneralSettings.AfterDownload == OnAfterDownload.BackToOriginalPlugin && properHandler.GetHasSubtitles(true)) {
                GUIWindowManager.ShowPreviousWindow();
            }
        }

        private void countDownloads(List<SubtitleDownloadStatus> statusList, out int succesful, out int canceled, out int errors) {
            succesful = 0;
            canceled = 0;
            errors = 0;

            if (statusList == null || statusList.Count == 0) return;

            foreach (SubtitleDownloadStatus subtitleDownloadStatus in statusList) {
                if (subtitleDownloadStatus.Status == SubtitleDownloadStatusStatus.Succesful)
                    succesful++;
                else if (subtitleDownloadStatus.Status == SubtitleDownloadStatusStatus.Canceled || subtitleDownloadStatus.Status == SubtitleDownloadStatusStatus.AlreadyExists)
                    canceled++;
                else
                    errors++;
            }
        }

        private void OnLanguageSelection() {
            List<MultiSelectionItem> selectedLanguages = null;

            selectedLanguages = SubCentralUtils.ShowMultiSelectionDialog(Localization.SelectLanguages, SubCentralUtils.getLanguageNamesForMultiSelection());

            if (selectedLanguages == null) return;

            while (SubCentralUtils.getSelectedLanguagesCountFromMultiSelection(selectedLanguages) < 1) {
                GUIUtils.ShowOKDialog(Localization.Error, Localization.PleaseSelectLanguages);

                selectedLanguages = SubCentralUtils.ShowMultiSelectionDialog(Localization.SelectLanguages, selectedLanguages);

                if (selectedLanguages == null) return;
            }

            if (SubCentralUtils.getSelectedLanguagesCountFromMultiSelection(selectedLanguages) > 0) {
                SubCentralUtils.setLanguageNamesFromMultiSelection(selectedLanguages);
            }
        }

        private void OnModifySearch() {
            if (CurrentHandler == null && SubCentralUtils.getEnabledAndValidFoldersForMedia(null, false).Count < 1) {
                GUIUtils.ShowOKDialog(Localization.Warning, Localization.CannotUseManualSearch);
                return;
            }

            ManualSearchHandler manualSearchHandler = core.PluginHandlers[PluginHandlerType.MANUAL] as ManualSearchHandler;

            // copy data from current handler
            if (CurrentHandler != manualSearchHandler && CurrentHandler != null) {
                manualSearchHandler.MediaDetail = CopyMediaDetail(CurrentHandler.MediaDetail);
                manualSearchHandler.Modified = false;
                manualSearchHandler.UpdateTags();
                ModifySearchSearchType = SubCentralUtils.getSubtitlesSearchTypeFromMediaDetail(manualSearchHandler.MediaDetail);
                if (ModifySearchSearchType == SubtitlesSearchType.NONE)
                    ModifySearchSearchType = SubtitlesSearchType.MOVIE;
            }
            else if (CurrentHandler != manualSearchHandler) {
                manualSearchHandler.UpdateTags();
                ModifySearchSearchType = SubtitlesSearchType.MOVIE;
            }

            _oldModifySearchSearchType = ModifySearchSearchType;

            CurrentHandler = manualSearchHandler;

            View = ViewMode.MODIFYSEARCH;
        }

        private BasicMediaDetail CopyMediaDetail(BasicMediaDetail mediaDetail) {
            List<FileInfo> fileList = new List<FileInfo>();
            if (mediaDetail.Files != null && mediaDetail.Files.Count > 0) {
                foreach (FileInfo fileInfo in CurrentHandler.MediaDetail.Files) {
                    try {
                        FileInfo fi = new FileInfo(fileInfo.FullName);
                        fileList.Add(fi);
                    }
                    catch {
                    }
                }
            }

            BasicMediaDetail newMediaDetail = new BasicMediaDetail();
            newMediaDetail.ImdbID = CurrentHandler.MediaDetail.ImdbID;
            newMediaDetail.Title = CurrentHandler.MediaDetail.Title;
            newMediaDetail.Year = CurrentHandler.MediaDetail.Year;
            newMediaDetail.AbsoluteNumbering = CurrentHandler.MediaDetail.AbsoluteNumbering;
            newMediaDetail.Season = CurrentHandler.MediaDetail.Season;
            newMediaDetail.Episode = CurrentHandler.MediaDetail.Episode;
            newMediaDetail.EpisodeAbs = CurrentHandler.MediaDetail.EpisodeAbs;
            newMediaDetail.Thumb = CurrentHandler.MediaDetail.Thumb;
            newMediaDetail.FanArt = CurrentHandler.MediaDetail.FanArt;
            newMediaDetail.Files = fileList;

            return newMediaDetail;
        }

        public static void ShowWaitCursor() {
            if (waitCursor == null) {
                waitCursor = new WaitCursor();
            }
        }

        public static void HideWaitCursor() {
            if (waitCursor != null) {
                waitCursor.Dispose();
                waitCursor = null;
            }
        }

        private void OnListSelection() {
            GUIListItem selectedItem = GUIControl.GetSelectedListItem(GetID, (int)GUIControls.PROVIDERSLIST);
            if (selectedItem != null && !selectedItem.IsRemote) {
                if (selectedItem.MusicTag is SettingsGroup || selectedItem.MusicTag is SettingsProvider) {
                    // search
                    _lastSelectedGroupsAndProvidersItemIndex = providerList.SelectedListItemIndex;
                    PerformSearch(selectedItem.MusicTag);
                }
                else if (selectedItem.MusicTag is SubtitleItem) {
                    _lastSelectedSubtitlesItemIndex = providerList.SelectedListItemIndex;
                    PerformDownload((SubtitleItem)selectedItem.MusicTag);
                }
                else {
                    // back
                    OnListBack();
                }
            }
        }

        private void OnListBack() {
            FillProviderGroupsAndProviders(true);
        }

        private void InitSearchProperties() {
            GUIUtils.SetProperty("#SubCentral.Search.Source.Text", string.Empty);
            GUIUtils.SetProperty("#SubCentral.Search.Source.Name", string.Empty);
            GUIUtils.SetProperty("#SubCentral.Search.Source.ID", string.Empty);
            GUIUtils.SetProperty("#SubCentral.Search.Files.Tags", string.Empty);

            List<string> sections = new List<string> { "Search", "ManualSearch" };

            foreach (string section in sections) {
                GUIUtils.SetProperty("#SubCentral." + section + ".Files.AllNames", string.Empty);
                for (int i = 1; i <= 10; i++) {
                    GUIUtils.SetProperty("#SubCentral." + section + ".Files.Name" + i, string.Empty);
                    GUIUtils.SetProperty("#SubCentral." + section + ".Files.FullName" + i, string.Empty);
                }

                GUIUtils.SetProperty("#SubCentral." + section + ".Media.Title", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.Year", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.TitleWithYear", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.IMDb.ID.Plain", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.IMDb.ID.Complete", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.IMDb.ID.Text", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.Season", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.Episode", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.TitleWithSE", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.AbsoluteNumbering", string.Empty);

                GUIUtils.SetProperty("#SubCentral." + section + ".Media.Thumb", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".Media.FanArt", string.Empty);

                GUIUtils.SetProperty("#SubCentral." + section + ".SearchType.Text", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".SearchType.NiceType", string.Empty);
                GUIUtils.SetProperty("#SubCentral." + section + ".SearchType.Type", string.Empty);
            }
        }

        private void PublishSearchProperties() {
            PublishSearchProperties(false);
        }

        private void PublishSearchProperties(bool manualSearch) {
            string section = "Search";

            if (manualSearch) {
                section = "ManualSearch";
            }
            else if (CurrentHandler != null) {
                GUIUtils.SetProperty("#SubCentral.Search.Source.Text", CurrentHandler.Type != PluginHandlerType.MANUAL ? string.Format(Localization.From, CurrentHandler.PluginName) : CurrentHandler.PluginName);
                GUIUtils.SetProperty("#SubCentral.Search.Source.Name", CurrentHandler.PluginName);
                GUIUtils.SetProperty("#SubCentral.Search.Source.ID", CurrentHandler.ID.ToString());

                if (CurrentHandler.TagRanking != null && CurrentHandler.TagRanking.MediaTags != null && CurrentHandler.TagRanking.MediaTags.AllTagsCombined != null) {
                    string tags = string.Empty;
                    foreach (string tag in CurrentHandler.TagRanking.MediaTags.AllTagsCombined) {
                        if (string.IsNullOrEmpty(tag)) continue;

                        if (string.IsNullOrEmpty(tags))
                            tags += tag;
                        else
                            tags += ", " + tag;
                    }
                    GUIUtils.SetProperty("#SubCentral.Search.Files.Tags", tags);
                }
            }
            else {
                InitSearchProperties();
                return;
            }

            BasicMediaDetail mediaDetail;
            if (manualSearch) {
                mediaDetail = _modifySearchMediaDetail;
            }
            else {
                mediaDetail = CurrentHandler.MediaDetail;
            }

            string allFiles = string.Empty;
            for (int i = 1; i <= 10; i++) {
                if (mediaDetail.Files != null && i <= mediaDetail.Files.Count) {
                    allFiles = allFiles + (string.IsNullOrEmpty(allFiles) ? mediaDetail.Files[i - 1].Name : "\n" + mediaDetail.Files[i - 1].Name);
                    GUIUtils.SetProperty("#SubCentral." + section + ".Files.Name" + i, mediaDetail.Files[i - 1].Name);
                    GUIUtils.SetProperty("#SubCentral." + section + ".Files.FullName" + i, mediaDetail.Files[i - 1].FullName);
                }
                else {
                    GUIUtils.SetProperty("#SubCentral." + section + ".Files.Name" + i, string.Empty);
                    GUIUtils.SetProperty("#SubCentral." + section + ".Files.FullName" + i, string.Empty);
                }
            }
            GUIUtils.SetProperty("#SubCentral." + section + ".Files.AllNames", allFiles);

            GUIUtils.SetProperty("#SubCentral." + section + ".Media.Title", mediaDetail.Title);
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.Year", mediaDetail.YearStr);
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.TitleWithYear", !string.IsNullOrEmpty(mediaDetail.YearStr) ? string.Format("{0} ({1})", mediaDetail.Title, mediaDetail.YearStr) : mediaDetail.Title);
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.IMDb.ID.Plain", mediaDetail.ImdbID);
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.IMDb.ID.Complete", mediaDetail.ImdbIDStr);
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.IMDb.ID.Text", string.Format(Localization.IMDbID, mediaDetail.ImdbIDStr));
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.Season", mediaDetail.SeasonStr);
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.Episode", mediaDetail.EpisodeStr);
            string SE = string.Empty;
            if (mediaDetail.AbsoluteNumbering && string.IsNullOrEmpty(mediaDetail.SeasonStr) && !string.IsNullOrEmpty(mediaDetail.EpisodeStr))
                SE = string.Format(" E{0:00}", mediaDetail.EpisodeStr);
            else if (!mediaDetail.AbsoluteNumbering && !string.IsNullOrEmpty(mediaDetail.SeasonStr) && !string.IsNullOrEmpty(mediaDetail.EpisodeStr))
                SE = string.Format(" S{0:00}E{1:00}", mediaDetail.SeasonStr, mediaDetail.EpisodeStr);
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.TitleWithSE", string.Format("{0}{1}", mediaDetail.Title, SE));
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.AbsoluteNumbering", mediaDetail.AbsoluteNumbering.ToString().ToLowerInvariant());
            
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.Thumb", mediaDetail.Thumb);
            GUIUtils.SetProperty("#SubCentral." + section + ".Media.FanArt", mediaDetail.FanArt);

            if (manualSearch) {
                GUIUtils.SetProperty("#SubCentral.ManualSearch.SearchType.Type", ModifySearchSearchType.ToString());
                if (SubCentralUtils.canSearchMediaDetailWithType(mediaDetail, ModifySearchSearchType)) {
                    GUIUtils.SetProperty("#SubCentral.ManualSearch.SearchType.Text", Localization.NotEnoughDataForSearch);
                }
            }
            else {
                SubtitlesSearchType searchType = getRealCurrentSearchSearchType(mediaDetail);
                switch (searchType) {
                    case SubtitlesSearchType.NONE:
                        GUIUtils.SetProperty("#SubCentral.Search.SearchType.Text", Localization.NotEnoughDataForSearch);
                        GUIUtils.SetProperty("#SubCentral.Search.SearchType.NiceType", searchType.ToString());
                        break;
                    case SubtitlesSearchType.TVSHOW:
                        GUIUtils.SetProperty("#SubCentral.Search.SearchType.Text", string.Format(Localization.SearchType, Localization.TVShow));
                        GUIUtils.SetProperty("#SubCentral.Search.SearchType.NiceType", Localization.TVShow);
                        break;
                    case SubtitlesSearchType.IMDb:
                        GUIUtils.SetProperty("#SubCentral.Search.SearchType.Text", string.Format(Localization.SearchType, Localization.MovieIMDb));
                        GUIUtils.SetProperty("#SubCentral.Search.SearchType.NiceType", Localization.MovieIMDb);
                        break;
                    case SubtitlesSearchType.MOVIE:
                        GUIUtils.SetProperty("#SubCentral.Search.SearchType.Text", string.Format(Localization.SearchType, Localization.Movie));
                        GUIUtils.SetProperty("#SubCentral.Search.SearchType.NiceType", Localization.Movie);
                        break;
                }
                GUIUtils.SetProperty("#SubCentral.Search.SearchType.Type", searchType.ToString());
            }
        }

        private SubtitlesSearchType getRealCurrentSearchSearchType(BasicMediaDetail mediaDetail) {
            SubtitlesSearchType searchType = SubCentralUtils.getSubtitlesSearchTypeFromMediaDetail(mediaDetail);

            if (ModifySearchSearchType != SubtitlesSearchType.NONE) {
                if (ModifySearchSearchType == SubtitlesSearchType.TVSHOW && SubCentralUtils.canSearchMediaDetailWithType(mediaDetail, ModifySearchSearchType))
                    searchType = SubtitlesSearchType.TVSHOW;
                else if (ModifySearchSearchType == SubtitlesSearchType.MOVIE && SubCentralUtils.canSearchMediaDetailWithType(mediaDetail, SubtitlesSearchType.IMDb))
                    searchType = SubtitlesSearchType.IMDb;
                else if (ModifySearchSearchType == SubtitlesSearchType.MOVIE && SubCentralUtils.canSearchMediaDetailWithType(mediaDetail, SubtitlesSearchType.MOVIE))
                    searchType = SubtitlesSearchType.MOVIE;
                else
                    searchType = SubtitlesSearchType.NONE;
            }

            return searchType;
        }

        private bool CheckAndTranslateSkin() {
            if (cancelButton != null && languagesButton != null && modifySearchButton != null && deleteButton != null && //sortButton != null &&
                providerList != null &&
                modifySearchOKButton != null && modifySearchCancelButton != null && modifySearchRevertButton != null && //modifySearchSelectFolderButton != null
                modifySearchMovieButton != null && modifySearchTVShowButton != null &&
                modifySearchIMDbIDButton != null &&
                modifySearchTitleButton != null && modifySearchYearButton != null &&
                modifySearchSeasonButton != null && modifySearchEpisodeButton != null &&
                modifySearchAbsoluteNumberingButton != null
                )
            {
                cancelButton.Label = Localization.Back;
                languagesButton.Label = Localization.Languages;
                modifySearchButton.Label = Localization.ModifySearch;
                deleteButton.Label = Localization.DeleteSubtitles;

                modifySearchOKButton.Label = Localization.OKSearch;
                modifySearchCancelButton.Label = Localization.Cancel;
                modifySearchRevertButton.Label = Localization.Revert;
                modifySearchClearFilesButton.Label = Localization.ClearMedia;

                modifySearchAbsoluteNumberingButton.Label = Localization.SkinTranslationAbsoluteNumbering;
                //modifySearchSelectFolderButton.Label = Localization.SelectDownloadFolder;

                return true;
            }
            else
                return false;
        }

        private void FillSubtitleSearchResults(List<SubtitleItem> subtitleItems) {
            if (subtitleItems != null && subtitleItems.Count > 0) {
                providerList.Clear();

                GUIListItem item = new GUIListItem();
                item.Label = "..";
                item.IsFolder = true;
                item.MusicTag = null;
                item.IconImage = "defaultSubtitlesFolderBack.png";
                providerList.Add(item);

                foreach (SubtitleItem subtitleItem in subtitleItems) {
                    SubtitlesSortDetails searchDetails = new SubtitlesSortDetails();
                    searchDetails.ListPosition = providerList.Count;
                    searchDetails.TagRank = 0.0;
                    bool subtitleFileNameMatchesMedia = false;
                    if (CurrentHandler != null && CurrentHandler.TagRanking != null) {
                        subtitleFileNameMatchesMedia = CurrentHandler.TagRanking.SubtitleFileNameMatchesMedia(subtitleItem.Subtitle.FileName);
                        searchDetails.TagRank = CurrentHandler.TagRanking.GetSubtitleFileRank(subtitleItem.Subtitle.FileName);
                    }
                    searchDetails.LanguagePriority = SubCentralUtils.getLanguagePriorityByCode(subtitleItem.Subtitle.LanguageCode);
                    searchDetails.Name = subtitleItem.Subtitle.FileName;
                    searchDetails.Provider = subtitleItem.ProviderTitle;
                    if (Settings.SettingsManager.Properties.GeneralSettings.UseLanguageCodeOnResults) {
                        searchDetails.Language = subtitleItem.Subtitle.LanguageCode;
                    }
                    else {
                        searchDetails.Language = subtitleItem.LanguageName;
                    }

                    //logger.Debug(string.Format("Subtitle {0} has list position of {1}", subtitleItem.Subtitle.FileName, searchDetails.ListPosition));
                    //logger.Debug(string.Format("Subtitle {0} has media tag rank of {1}", subtitleItem.Subtitle.FileName, searchDetails.TagRank));
                    //logger.Debug(string.Format("Subtitle {0} has language priority of {1}", subtitleItem.Subtitle.FileName, searchDetails.LanguagePriority));

                    GUIListItem providerListItem = new GUIListItem();
                    providerListItem.IsFolder = false;
                    providerListItem.Label = subtitleItem.Subtitle.FileName;
                    providerListItem.Label2 = subtitleItem.ProviderTitle;
                    if (Settings.SettingsManager.Properties.GeneralSettings.UseLanguageCodeOnResults) {
                        providerListItem.Label3 = subtitleItem.Subtitle.LanguageCode;
                    }
                    else {
                        providerListItem.Label3 = subtitleItem.LanguageName;
                    }
                    providerListItem.MusicTag = subtitleItem;
                    providerListItem.AlbumInfoTag = searchDetails;

                    providerListItem.IconImage = "defaultSubtitles.png";

                    if (subtitleFileNameMatchesMedia)
                        providerListItem.IsPlayed = true;

                    providerList.Add(providerListItem);
                }

                ListControlViewState = ListControlViewState.SEARCHRESULTS;

                providerList.SelectedListItemIndex = _lastSelectedSubtitlesItemIndex;

                GUIUtils.SetProperty("#itemcount", string.Concat((providerList.ListItems.Count - 1).ToString(), " ", Localization.SubtitleS));

                OnSort();
            }
            else {
                GUIUtils.ShowNotifyDialog(Localization.SubtitleSearch, Localization.NoResultsFound, GUIUtils.NoSubtitlesLogoThumbPath);
            }
        }

        private void FillProviderGroupsAndProviders(bool defaultEnabled) {
            providerList.Clear();

            int groupCounter = 0;
            int providerCounter = 0;

            foreach (SettingsGroup settingsGroup in SubCentralUtils.getEnabledProviderGroups()) {
                if (SubCentralUtils.groupHasEnabledProviders(settingsGroup)) {
                    GUIListItem groupListItem = new GUIListItem();
                    groupListItem.IsFolder = true;
                    groupListItem.Label = settingsGroup.Title;
                    groupListItem.MusicTag = settingsGroup;
                    groupListItem.IsRemote = !defaultEnabled;

                    groupListItem.IconImage = "defaultSubtitlesFolder.png";
                    //MediaPortal.Util.Utils.SetDefaultIcons(groupListItem);

                    providerList.Add(groupListItem);
                    groupCounter++;
                }
            }

            foreach (SettingsProvider settingsProvider in SubCentralUtils.getEnabledProviders()) {
                GUIListItem providerListItem = new GUIListItem();
                providerListItem.IsFolder = false;
                providerListItem.Label = settingsProvider.Title;
                providerListItem.MusicTag = settingsProvider;
                providerListItem.IsRemote = !defaultEnabled;
                providerListItem.IconImage = "defaultSubtitles.png";

                providerList.Add(providerListItem);
                providerCounter++;
            }

            ListControlViewState = ListControlViewState.GROUPSANDPROVIDERS;

            providerList.SelectedListItemIndex = _lastSelectedGroupsAndProvidersItemIndex;
            _lastSelectedGroupsAndProvidersItemIndex = 0;
            _lastSelectedSubtitlesItemIndex = 1;

            if (groupCounter > 0 && providerCounter > 0) {
                GUIUtils.SetProperty("#itemcount", string.Concat(string.Concat(groupCounter.ToString(), " ", Localization.GroupS), ", ", string.Concat(providerCounter.ToString(), " ", Localization.ProviderS)));
            }
            else if (groupCounter > 0) {
                GUIUtils.SetProperty("#itemcount", string.Concat(groupCounter.ToString(), " ", Localization.GroupS));
            }
            else if (providerCounter > 0) {
                GUIUtils.SetProperty("#itemcount", string.Concat(providerCounter.ToString(), " ", Localization.ProviderS));
            }
        }

        private void EnableDisableProviderGroupsAndProviders(bool enabled) {
            foreach (GUIListItem item in providerList.ListItems) {
                item.IsRemote = !enabled;
            }
        }

        private void ModifyAbsoluteNumbering() {
            _modifySearchMediaDetail.AbsoluteNumbering = !_modifySearchMediaDetail.AbsoluteNumbering;

            if (!_modifySearchMediaDetail.AbsoluteNumbering)  
                GUIControl.EnableControl(GetID, modifySearchSeasonButton.GetID);
            else
                GUIControl.DisableControl(GetID, modifySearchSeasonButton.GetID);

            PublishSearchProperties(true);
        }

        private void ClearFiles() {
            if (SubCentralUtils.getEnabledAndValidFoldersForMedia(null, false).Count < 1) {
                GUIUtils.ShowOKDialog(Localization.Warning, Localization.CannotClearMedia);
                return;
            }

            _clearedMedia = true;

            if (_modifySearchMediaDetail.Files != null) {
                _modifySearchMediaDetail.Files.Clear();
                GUIUtils.SetProperty("#SubCentral.Search.Files.Tags", string.Empty);
                if (CurrentHandler != null)
                    CurrentHandler.UpdateTags();
            }

            _modifySearchMediaDetail.FanArt = string.Empty;
            _modifySearchMediaDetail.Thumb = string.Empty;

            // these will be set temporary, on cancel or revert they will be restored, on ok cleared again
            GUIUtils.SetProperty("#SubCentral.Search.Media.FanArt", string.Empty);
            GUIUtils.SetProperty("#SubCentral.Search.Media.Thumb", string.Empty);

            GUIControl.DisableControl(GetID, deleteButton.GetID);

            modifySearchClearFilesButton.Visible = false;
            //modifySearchSelectFolderButton.Visible = true;
            //GUIControl.FocusControl(GetID, (int)GUIControls.MODIFYSEARCHSELECTFOLDERBUTTON);
            GUIControl.FocusControl(GetID, (int)GUIControls.MODIFYSEARCHOKBUTTON);

            PublishSearchProperties(true);
        }

        private void ModifySearchWithUserData() {
            //CurrentHandler.MediaDetail = CopyMediaDetail(_modifySearchMediaDetail);
            CurrentHandler.MediaDetail = _modifySearchMediaDetail;
            _lastSelectedGroupsAndProvidersItemIndex = 0;
            //if (SubCentralUtils.getSubtitlesSearchTypeFromMediaDetail(CurrentHandler.MediaDetail) != SubtitlesSearchType.NONE)
            //if (SubCentralUtils.canSearchMediaDetailWithType(CurrentHandler.MediaDetail, ModifySearchSearchType))
            SubtitlesSearchType searchType = getRealCurrentSearchSearchType(CurrentHandler.MediaDetail);
            if (searchType != SubtitlesSearchType.NONE) {
                FillProviderGroupsAndProviders(true);
            }
            else {
                FillProviderGroupsAndProviders(false);
            }
            View = ViewMode.SEARCH;

            if (Settings.SettingsManager.Properties.GeneralSettings.PluginLoadWithSearchData == OnPluginLoadWithSearchData.SearchDefaults &&
                Settings.SettingsManager.Properties.GeneralSettings.SearchDefaultsWhenFromManualSearch) {
                SettingsGroup defaultGroup = SubCentralUtils.getDefaultGroupForSearchType(searchType);
                if (defaultGroup != null) {
                    _lastSelectedGroupsAndProvidersItemIndex = LocateGroupInListControl(defaultGroup);
                    PerformSearch(defaultGroup);
                }
            }
        }

        private void CancelModifySearch() {
            if (CurrentHandler.Modified) {
                if (_clearedMedia) {
                    if (_shouldDeleteButtonVisible)
                        GUIControl.EnableControl(GetID, deleteButton.GetID);
                    else
                        GUIControl.DisableControl(GetID, deleteButton.GetID);
                }
                ModifySearchSearchType = _oldModifySearchSearchType;
                View = ViewMode.SEARCH;
            }
            else {
                RevertModifySearch(false);
            }
        }

        private void RevertModifySearch(bool revertProviderList) {
            CurrentHandler.Clear();
            ModifySearchSearchType = SubtitlesSearchType.NONE;
            _oldModifySearchSearchType = SubtitlesSearchType.NONE;
            CurrentHandler = _backupHandler;
            _lastSelectedGroupsAndProvidersItemIndex = 0;
            if (_shouldDeleteButtonVisible)
                GUIControl.EnableControl(GetID, deleteButton.GetID);
            else
                GUIControl.DisableControl(GetID, deleteButton.GetID);

            if (CurrentHandler == null) {
                View = ViewMode.MAIN;
                return;
            }
            //else if (SubCentralUtils.getSubtitlesSearchTypeFromMediaDetail(CurrentHandler.MediaDetail) != SubtitlesSearchType.NONE) {
            if (revertProviderList) {
                if (getRealCurrentSearchSearchType(CurrentHandler.MediaDetail) != SubtitlesSearchType.NONE) {
                    FillProviderGroupsAndProviders(true);
                }
                else {
                    FillProviderGroupsAndProviders(false);
                }
            }
            View = ViewMode.SEARCH;
        }

        private void KeyboardModifySearch(int controlId) {
            string fillWith = string.Empty;
            switch (controlId) {
                case (int)GUIControls.MODIFYSEARCHIMDBIDBUTTON:
                    fillWith = _modifySearchMediaDetail.ImdbIDStr ?? string.Empty;
                    break;
                case (int)GUIControls.MODIFYSEARCHTITLEBUTTON:
                    fillWith = _modifySearchMediaDetail.Title ?? string.Empty;
                    break;
                case (int)GUIControls.MODIFYSEARCHYEARBUTTON:
                    fillWith = _modifySearchMediaDetail.Year == 0 ? string.Empty : _modifySearchMediaDetail.Year.ToString();
                    break;
                case (int)GUIControls.MODIFYSEARCHSEASONBUTTON:
                    fillWith = _modifySearchMediaDetail.SeasonProper == 0 ? string.Empty : _modifySearchMediaDetail.SeasonProper.ToString();
                    break;
                case (int)GUIControls.MODIFYSEARCHEPISODEBUTTON:
                    fillWith = _modifySearchMediaDetail.EpisodeProper == 0 ? string.Empty : _modifySearchMediaDetail.EpisodeProper.ToString();
                    break;
            }

            bool inputConfirmed = false;
            bool inputCorrect = false;

            while (!inputCorrect) {
                inputCorrect = true;

                inputConfirmed = GUIUtils.GetStringFromKeyboard(ref fillWith);

                if (!inputConfirmed || string.IsNullOrEmpty(fillWith)) break;

                switch (controlId) {
                    case (int)GUIControls.MODIFYSEARCHIMDBIDBUTTON:
                        if (fillWith != "tt" && !SubCentralUtils.isImdbIdCorrect(fillWith)) {
                            inputCorrect = false;
                            GUIUtils.ShowNotifyDialog(Localization.Error, Localization.WrongFormatIMDbID);
                        }
                        break;
                    case (int)GUIControls.MODIFYSEARCHTITLEBUTTON:
                        break;
                    case (int)GUIControls.MODIFYSEARCHYEARBUTTON:
                        if (!SubCentralUtils.isYearCorrect(fillWith)) {
                            inputCorrect = false;
                            GUIUtils.ShowNotifyDialog(Localization.Error, string.Format(Localization.WrongFormatYear, SubCentralUtils.yearToRange()));
                        }
                        break;
                    case (int)GUIControls.MODIFYSEARCHSEASONBUTTON:
                        if (!SubCentralUtils.isSeasonOrEpisodeCorrect(fillWith, false)) {
                            inputCorrect = false;
                            GUIUtils.ShowNotifyDialog(Localization.Error, string.Format(Localization.WrongFormatSeasonEpisode, Localization.SkinTranslationSeason));
                        }
                        break;
                    case (int)GUIControls.MODIFYSEARCHEPISODEBUTTON:
                        if (!SubCentralUtils.isSeasonOrEpisodeCorrect(fillWith, _modifySearchMediaDetail.AbsoluteNumbering)) {
                            inputCorrect = false;
                            GUIUtils.ShowNotifyDialog(Localization.Error, string.Format(Localization.WrongFormatSeasonEpisode, Localization.SkinTranslationEpisode));
                        }
                        break;
                }
            }

            if (inputConfirmed) {
                switch (controlId) {
                    case (int)GUIControls.MODIFYSEARCHIMDBIDBUTTON:
                        _modifySearchMediaDetail.ImdbID = fillWith;
                        break;
                    case (int)GUIControls.MODIFYSEARCHTITLEBUTTON:
                        _modifySearchMediaDetail.Title = fillWith.ToTitleCase();
                        break;
                    case (int)GUIControls.MODIFYSEARCHYEARBUTTON:
                        int year = 0;
                        int.TryParse(fillWith, out year);
                        _modifySearchMediaDetail.Year = year;
                        break;
                    case (int)GUIControls.MODIFYSEARCHSEASONBUTTON:
                        int season = 0;
                        int.TryParse(fillWith, out season);
                        _modifySearchMediaDetail.Season = season;
                        break;
                    case (int)GUIControls.MODIFYSEARCHEPISODEBUTTON:
                        int episode = 0;
                        int.TryParse(fillWith, out episode);
                        _modifySearchMediaDetail.EpisodeProper = episode;
                        break;
                }
            }
        }

        private void OnShowDialogSortOptions() {
            List<GUIListItem> items = new List<GUIListItem>();
            items.Add(new GUIListItem(Localization.GroupProviderDefault));
            items.Add(new GUIListItem(Localization.SortByMediaTags));
            items.Add(new GUIListItem(Localization.SortByLanguage));
            items.Add(new GUIListItem(Localization.SortByName));

            int dlgResult = GUIUtils.ShowMenuDialog(Localization.Sorting, items, (int)_subtitlesSortMethod);

            if (dlgResult < 0) return;

            switch (dlgResult) {
                case 0:
                    _subtitlesSortMethod = SubtitlesSortMethod.DefaultNoSort;
                    break;
                case 1:
                    _subtitlesSortMethod = SubtitlesSortMethod.MediaTags;
                    break;
                case 2:
                    _subtitlesSortMethod = SubtitlesSortMethod.SubtitleLanguage;
                    break;
                case 3:
                    _subtitlesSortMethod = SubtitlesSortMethod.SubtitleName;
                    break;
            }

            OnSort();
            GUIControl.FocusControl(GetID, (int)GUIControls.SORTBUTTON);
        }

        private void OnSort() {
            providerList.Sort(new GUIListItemSubtitleComparer(_subtitlesSortMethod, _subtitlesSortAsc));

            UpdateSortButton();
        }

        private void UpdateSortButton() {
            string labelSort = Localization.Sort;

            if (View == ViewMode.SEARCH && ListControlViewState == ListControlViewState.SEARCHRESULTS) {
                switch (_subtitlesSortMethod) {
                    case SubtitlesSortMethod.DefaultNoSort:
                        labelSort = string.Format(Localization.SortBy, Localization.Default);
                        break;
                    case SubtitlesSortMethod.MediaTags:
                        labelSort = string.Format(Localization.SortBy, Localization.SkinTranslationMediaTags);
                        break;
                    case SubtitlesSortMethod.SubtitleLanguage:
                        labelSort = string.Format(Localization.SortBy, Localization.Language);
                        break;
                    case SubtitlesSortMethod.SubtitleName:
                        labelSort = string.Format(Localization.SortBy, Localization.Name);
                        break;
                }
                if (sortButton != null)
                    sortButton.Disabled = false;
            }
            else {
                if (sortButton != null)
                    sortButton.Disabled = true;
            }
            if (sortButton != null) {
                sortButton.Label = labelSort;
                sortButton.IsAscending = _subtitlesSortAsc;
            }
        }

        private void OnSortChanged(object sender, SortEventArgs e) {
            _subtitlesSortAsc = e.Order != SortOrder.Descending;
            OnSort();
            GUIControl.FocusControl(GetID, (int)GUIControls.SORTBUTTON);
        }

        private void UpdateButtonStates() {
            cancelButton.Disabled = View == ViewMode.MODIFYSEARCH;
            languagesButton.Disabled = View == ViewMode.MODIFYSEARCH;
            modifySearchButton.Disabled = View == ViewMode.MODIFYSEARCH;
            UpdateSortButton();
            if (sortButton != null)
                sortButton.Disabled = sortButton.Disabled || View == ViewMode.MODIFYSEARCH;
        }

        private int LocateGroupInListControl(SettingsGroup defaultGroup) {
            int result = -1;

            if (providerList.ListItems == null || providerList.ListItems.Count == 0) return result;

            for (int i = 0; i < providerList.ListItems.Count; i++) {
                GUIListItem item = providerList.ListItems[i];
                if (item.IsFolder && item.MusicTag != null && item.MusicTag is SettingsGroup && ((SettingsGroup)item.MusicTag).Equals(defaultGroup))
                    return i;
            }

            return result;
        }

        private void AskAndFalseHasSubtitles(PluginHandler properHandler, string baseText, bool defaultYes) {
            string question = baseText;
            if (!_mediaAvailable && properHandler.GetEmbeddedSubtitles() < 0)
                question += "\n" + Localization.MediaMaybeInternalSubtitles;
            if (GUIUtils.ShowYesNoDialog(Localization.Warning, string.Format(question, properHandler == null ? Localization.ExternalPlugin : properHandler.PluginName), defaultYes)) {
                foreach (FileInfo fi in properHandler.MediaDetail.Files) {
                    properHandler.SetHasSubtitles(fi.FullName, false);
                }
            }
            _subtitlesExistForCurrentMedia = false;
        }

        private void AskAndTrueHasSubtitles(PluginHandler properHandler, string baseText, bool defaultYes) {
            string question = baseText;
            if (GUIUtils.ShowYesNoDialog(Localization.Warning, string.Format(question, properHandler == null ? Localization.ExternalPlugin : properHandler.PluginName), defaultYes)) {
                foreach (FileInfo fi in properHandler.MediaDetail.Files) {
                    properHandler.SetHasSubtitles(fi.FullName, true);
                }
            }
            _subtitlesExistForCurrentMedia = true;
        }

        private bool CheckMediaForSubtitlesOnOpen(PluginHandler properHandler) {
            return CheckMediaForSubtitlesOnOpen(properHandler, false);
        }

        private bool CheckMediaForSubtitlesOnOpen(PluginHandler properHandler, bool questionsOnly) {
            bool resultContinue = true;

            if (properHandler == null) return resultContinue;

            if (!_checkMediaForSubtitlesOnOpenDone) {
                _subtitlesExistForCurrentMedia = SubCentralUtils.MediaHasSubtitles(properHandler.MediaDetail.Files, true, properHandler.GetEmbeddedSubtitles(), false, ref _subtitleFilesForCurrentMedia);
                _mediaAvailable = FileUtils.mediaIsAvailable(properHandler.MediaDetail.Files);
                _checkMediaForSubtitlesOnOpenDone = true;
            }

            if (_subtitlesExistForCurrentMedia && !properHandler.GetHasSubtitles(false) && properHandler.Type == PluginHandlerType.BASIC) {
                AskAndTrueHasSubtitles(properHandler, Localization.MediaWrongMarkNoSubtitles, false);
            }
            else if (!questionsOnly && _subtitlesExistForCurrentMedia) {
                //GUIUtils.ShowNotifyDialog(Localization.Warning, string.Format(Localization.MediaHasSubtitles, CurrentHandler == null ? Localization.ExternalPlugin : CurrentHandler.PluginName));
                GUIUtils.ShowNotifyDialog(Localization.Warning, Localization.MediaHasSubtitles);
            }
            else if (!_subtitlesExistForCurrentMedia && properHandler.GetHasSubtitles(false)) {
                AskAndFalseHasSubtitles(properHandler, Localization.MediaWrongMarkHasSubtitles, false);
                resultContinue = false;
            }
            
            return resultContinue;
        }

        private void OnDeleteSubtitles() {
            PluginHandler properHandler = _backupHandler != null ? _backupHandler : CurrentHandler;
            if (properHandler == null) return;
            if (properHandler.MediaDetail.Files == null || properHandler.MediaDetail.Files.Count == 0) return;

            if (!CheckMediaForSubtitlesOnOpen(properHandler, true)) return;

            if (!_subtitlesExistForCurrentMedia)
                // nothing
                GUIUtils.ShowNotifyDialog(Localization.Error, Localization.MediaNoSubtitles);
            else if (_subtitlesExistForCurrentMedia && _subtitleFilesForCurrentMedia.Count < 1) {
                // only embedded
                GUIUtils.ShowNotifyDialog(Localization.Error, Localization.MediaOnlyInternalSubtitles);
            }
            else {
                // finally some work to do

                // first get local folders
                List<string> localFolders = new List<string>();
                foreach (FileInfo mediaFile in properHandler.MediaDetail.Files)
                    localFolders.Add(mediaFile.DirectoryName);

                int subtitleToDeleteIndex = int.MaxValue;
                while (subtitleToDeleteIndex > 1) { // repeat only if user didn't select cancel (-1), all (0) or local (1)
                    List<GUIListItem> menuItems = new List<GUIListItem>();
                    menuItems.Add(new GUIListItem(Localization.All));
                    menuItems.Add(new GUIListItem(Localization.Local));
                    foreach (FileInfo fiSubtitle in _subtitleFilesForCurrentMedia) {
                        GUIListItem item = new GUIListItem(fiSubtitle.FullName);
                        item.MusicTag = fiSubtitle;
                        menuItems.Add(item);
                    }

                    subtitleToDeleteIndex = GUIUtils.ShowMenuDialog(Localization.SubtitleFilesToDelete, menuItems, 2);

                    if (subtitleToDeleteIndex > -1) {
                        if (subtitleToDeleteIndex == 0) {
                            // delete all
                            List<SubtitleDeleteStatus> statusList = DeleteSubtitles(_subtitleFilesForCurrentMedia, localFolders, false);
                            List<string> errorList = (from status in statusList where status.Status == SubtitleDeleteStatusStatus.Error select status.FullFileName).ToList();
                            if (errorList.Count > 0) {
                                GUIUtils.ShowTextDialog(Localization.UnableToDeleteSubtitleFiles, errorList);
                                subtitleToDeleteIndex = int.MaxValue;
                            }
                            else if (statusList.Count < 1) {
                                GUIUtils.ShowNotifyDialog(Localization.Error, Localization.NoSubtitlesDelete);
                            }
                            else {
                                GUIUtils.ShowNotifyDialog(Localization.Completed, string.Format(Localization.AllSubtitlesDeleted, statusList.Count), GUIUtils.NoSubtitlesLogoThumbPath);
                            }
                        }
                        else if (subtitleToDeleteIndex == 1) {
                            // delete local
                            List<SubtitleDeleteStatus> statusList = DeleteSubtitles(_subtitleFilesForCurrentMedia, localFolders, true);
                            List<string> errorList = (from status in statusList where status.Status == SubtitleDeleteStatusStatus.Error select status.FullFileName).ToList();
                            if (errorList.Count > 0) {
                                GUIUtils.ShowTextDialog(Localization.UnableToDeleteSubtitleFiles, errorList);
                                subtitleToDeleteIndex = int.MaxValue;
                            }
                            else if (statusList.Count < 1) {
                                GUIUtils.ShowNotifyDialog(Localization.Error, Localization.NoSubtitlesDelete);
                            }
                            else {
                                GUIUtils.ShowNotifyDialog(Localization.Completed, string.Format(Localization.AllSubtitlesDeleted, statusList.Count), GUIUtils.NoSubtitlesLogoThumbPath);
                            }
                        }
                        else if (_subtitleFilesForCurrentMedia[subtitleToDeleteIndex - 2].Exists) {
                            try {
                                _subtitleFilesForCurrentMedia[subtitleToDeleteIndex - 2].Delete();
                                _subtitleFilesForCurrentMedia.RemoveAt(subtitleToDeleteIndex - 2);
                            }
                            catch (Exception e) {
                                logger.WarnException(string.Format("Unable to delete subtitle file {0}", _subtitleFilesForCurrentMedia[subtitleToDeleteIndex - 2].FullName), e);
                                GUIUtils.ShowNotifyDialog(Localization.Error, Localization.UnableToDeleteSubtitleFile);
                            }
                        }

                        if (_subtitleFilesForCurrentMedia.Count < 1 && properHandler.GetHasSubtitles(false) && properHandler.Type == PluginHandlerType.BASIC) {
                            AskAndFalseHasSubtitles(properHandler, Localization.MediaNoMoreSubtitles, true);
                            subtitleToDeleteIndex = -1;
                        }
                    }
                }
            }
        }

        private List<SubtitleDeleteStatus> DeleteSubtitles(List<FileInfo> subtitleFilesForCurrentMedia, List<string> localFolders, bool useLocalOnly) {
            List<SubtitleDeleteStatus> result = new List<SubtitleDeleteStatus>();

            if (subtitleFilesForCurrentMedia == null || subtitleFilesForCurrentMedia.Count < 1) return result;

            for (int i = 0; i < subtitleFilesForCurrentMedia.Count; i++) {
                FileInfo fiSubtitle = subtitleFilesForCurrentMedia[i];
                SubtitleDeleteStatus status = new SubtitleDeleteStatus() { FullFileName = fiSubtitle.FullName };

                try {
                    if (!useLocalOnly || (useLocalOnly && localFolders != null && localFolders.Contains(fiSubtitle.DirectoryName))) {
                        if (fiSubtitle.Exists)
                            fiSubtitle.Delete();
                        status.Status = SubtitleDeleteStatusStatus.Succesful;
                        subtitleFilesForCurrentMedia.RemoveAt(i);
                        result.Add(status);
                        i--;
                    }
                }
                catch (Exception e) {
                    logger.WarnException(string.Format("Unable to delete subtitle file {0}", fiSubtitle.FullName), e);
                    status.Status = SubtitleDeleteStatusStatus.Error;
                    result.Add(status);
                }
            }

            return result;
        }

        #endregion
    }

    public enum ViewMode {
        NONE,
        MAIN,
        SEARCH,
        MODIFYSEARCH
    }

    public enum ListControlViewState {
        GROUPSANDPROVIDERS,
        SEARCHRESULTS
    }
}
