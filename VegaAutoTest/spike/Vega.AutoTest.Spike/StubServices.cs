// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

// 本文件由 StubGenerator.GenerateStubs 反射生成，请勿手工编辑。
#nullable disable
#pragma warning disable CS0067 // 替身不触发事件

namespace Vega.AutoTest.Spike;

public sealed class StubNavigationService : global::Galaxy.Platform.INavigations.INavigationService
{
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnNavigateTo;
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnRename;
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnCanSaveChanged;
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnCanCloseChanged;
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnClose;
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnCloseTab;
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnCloseSubViews;
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnCloseStandaloneViews;
    public event global::System.EventHandler<global::Galaxy.Platform.INavigations.NavigationEventArgs> OnActiveViewChanged;
    public global::System.Boolean NavigateTo(global::System.String p0, global::System.Type p1, global::Galaxy.Platform.INavigations.DockingRoles p2, global::System.Object[] p3)
    {
        return default;
    }
    public void NavigateToDetached(global::System.String p0, global::System.Type p1, global::System.Object[] p2)
    {
    }
    public void CloseStandaloneForcelly(global::System.String p0)
    {
    }
    public void CloseRelatedStandaloneForcelly(global::System.String p0)
    {
    }
    public void CloseAllStandalone()
    {
    }
    public void Rename(global::System.String p0, global::System.String p1)
    {
    }
    public global::System.Boolean ConfirmSaveTab(global::Galaxy.Platform.INavigations.ICanSave p0)
    {
        return default;
    }
    public global::System.Boolean Close(global::System.String p0, global::System.Boolean p1, global::System.Boolean p2)
    {
        return default;
    }
    public global::System.Boolean CloseRelatedTabs(global::System.String p0, global::System.Boolean p1, global::System.Boolean p2)
    {
        return default;
    }
    public global::System.Boolean CloseAllTabs(global::System.Boolean p0, global::System.Boolean p1)
    {
        return default;
    }
    public global::System.String GetActiveView()
    {
        return default;
    }
    public void SetActiveView(global::System.String p0)
    {
    }
    public void SaveActiveView()
    {
    }
    public void SaveAsActiveView()
    {
    }
    public void SaveAllViews()
    {
    }
    public global::System.Boolean HasView(global::System.String p0)
    {
        return default;
    }
    public global::System.Boolean HasRelatedTabs(global::System.String p0)
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::System.String> GetRelatedTabs(global::System.String p0)
    {
        return default;
    }
    public global::Galaxy.Platform.INavigations.InteractionResult ShowXDialog(global::System.Type p0, global::System.Object[] p1)
    {
        return default;
    }
    public void ShowXWindow(global::System.Type p0, global::System.Object[] p1)
    {
    }
    public global::System.Threading.Tasks.Task<global::Galaxy.Platform.INavigations.InteractionResult> ShowXDialogAsync(global::System.Type p0, global::System.Object[] p1)
    {
        return default;
    }
    public global::System.Threading.Tasks.Task ShowXWindowAsync(global::System.Type p0, global::System.Object[] p1)
    {
        return default;
    }
    public global::System.Boolean HasUnsavedView(global::System.String p0)
    {
        return default;
    }
    public global::System.Boolean IsStandalone()
    {
        return default;
    }
    public void SetStandalone(global::System.Boolean p0)
    {
    }
    public void RemoveFromStack(global::System.String p0)
    {
    }
}

public sealed class StubConfigsManager : global::Vega.Utility.Common.IConfigsManager
{
    public global::Vega.Utility.Common.WaferHandlerConfig HandlerConfigs { get; set; }
    public global::Vega.Utility.Common.GeneralConfig GeneralConfig { get; set; }
    public global::Vega.Utility.Common.PerformanceMonitorConfig MonitorConfig { get; set; }
    public global::Vega.Utility.Common.AnalysisConfig AnalysisConfig { get; set; }
    public global::Vega.Utility.Common.DataCollectorConfig DataCollectorConfig { get; set; }
    public global::Vega.Utility.Common.WaferRunConfig WaferRunConfig { get; set; }
    public global::Vega.Utility.Common.HardWareMotorsConfig HardWareMotorsConfig { get; set; }
    public global::Vega.Utility.Common.HardWareMotionConfig HardWareMotionConfig { get; set; }
    public global::Vega.Utility.Common.EdgeDetectionConfig EdgeDetectionConfig { get; set; }
    public global::Vega.Utility.Common.HardWareFFUConfig HardWareFFUConfig { get; set; }
    public global::Vega.Utility.Common.CommunicationConfig CommunicationConfig { get; set; }
    public global::Vega.Utility.Common.CalibrationConfig CalibrationConfig { get; set; }
    public global::Vega.Utility.Common.IOControllerConfig IOControllerConfig { get; set; }
    public global::Vega.Utility.Common.DewpointDeviceConfig DewpointDeviceConfig { get; set; }
    public global::Vega.Utility.Common.LaserConfig LaserConfig { get; set; }
    public global::Vega.Utility.Common.MFCConfig MFCConfig { get; set; }
    public global::Vega.Utility.Common.SignalTowerConfig SignalTowerConfig { get; set; }
    public global::Vega.Utility.Common.OpticsConfig OpticsConfig { get; set; }
    public global::Vega.Utility.Common.AutoFocusConfig AutoFocusConfig { get; set; }
    public global::Vega.Utility.Common.SurfaceQualityThresholdConfig SurfaceQualityConfig { get; set; }
    public void SaveConfigs()
    {
    }
    public global::System.Boolean Set<TValue>(global::System.Linq.Expressions.Expression<global::System.Func<global::Vega.Utility.Common.IConfigsManager, TValue>> p0, TValue p1)
    {
        return default;
    }
}

public sealed class StubCalibrationService : global::Galaxy.Platform.ICalibrations.ICalibrationService
{
    public global::Galaxy.Platform.ICalibrations.CalibrationQueue RunQueue { get; }
    public global::System.Boolean IsAutoStart { get; set; }
    public global::System.Boolean IsRunning { get; set; }
    public global::Galaxy.Platform.ICalibrations.ICalibration CurrentCalibration { get; set; }
    public global::Galaxy.Platform.ICalibrations.ICalibration GetCalibration(global::System.String p0)
    {
        return default;
    }
    public global::Galaxy.Data.Calibrations.ICalibrationConfig GetConfig(global::System.String p0)
    {
        return default;
    }
    public void SaveCaliConfig(global::Galaxy.Data.Calibrations.ICalibrationConfig p0)
    {
    }
    public void SaveCaliResult(global::Galaxy.Data.Calibrations.CalibrationResultBase p0)
    {
    }
    public void DeleteCaliResult(global::Galaxy.Data.Calibrations.CalibrationResultBase p0)
    {
    }
    public global::System.Boolean Export(global::Galaxy.Data.Calibrations.CalibrationResultBase p0, global::System.String p1)
    {
        return default;
    }
    public global::System.Threading.Tasks.Task<global::System.Boolean> ExportAsync(global::Galaxy.Data.Calibrations.CalibrationResultBase p0, global::System.String p1)
    {
        return default;
    }
    public global::Galaxy.Data.Calibrations.CalibrationResultBase Import(global::System.String p0)
    {
        return default;
    }
    public global::System.Threading.Tasks.Task<global::Galaxy.Data.Calibrations.CalibrationResultBase> ImportAsync(global::System.String p0)
    {
        return default;
    }
    public global::Galaxy.Data.Calibrations.CalibrationResultBase GetLatestResult(global::System.String p0)
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::Galaxy.Data.Calibrations.CalibrationResultBase> GetResults(global::System.String p0)
    {
        return default;
    }
    public global::Galaxy.Data.Calibrations.CalibrationResultBase Calibrate(global::System.String p0, global::Galaxy.Data.Calibrations.ICalibrationConfig p1)
    {
        return default;
    }
    public global::Galaxy.Data.Calibrations.CalibrationResultBase Calibrate<T>(global::System.String p0, global::Galaxy.Data.Calibrations.ICalibrationConfig p1, global::System.IProgress<T> p2, global::System.Threading.CancellationToken p3)
    {
        return default;
    }
    public global::System.Threading.Tasks.Task<global::Galaxy.Data.Calibrations.CalibrationResultBase> CalibrateAsync(global::System.String p0, global::Galaxy.Data.Calibrations.ICalibrationConfig p1)
    {
        return default;
    }
    public global::System.Threading.Tasks.Task<global::Galaxy.Data.Calibrations.CalibrationResultBase> CalibrateAsync<T>(global::System.String p0, global::Galaxy.Data.Calibrations.ICalibrationConfig p1, global::System.IProgress<T> p2, global::System.Threading.CancellationToken p3)
    {
        return default;
    }
    public global::Galaxy.Data.Calibrations.CalibrationResultBase CalibrateOffline<T>(global::System.String p0, global::Galaxy.Data.Calibrations.ICalibrationConfig p1, global::Galaxy.Data.Calibrations.ICaliOfflineConfig p2, global::System.IProgress<T> p3, global::System.Threading.CancellationToken p4)
    {
        return default;
    }
    public global::System.Threading.Tasks.Task<global::Galaxy.Data.Calibrations.CalibrationResultBase> CalibrateOfflineAsync<T>(global::System.String p0, global::Galaxy.Data.Calibrations.ICalibrationConfig p1, global::Galaxy.Data.Calibrations.ICaliOfflineConfig p2, global::System.IProgress<T> p3, global::System.Threading.CancellationToken p4)
    {
        return default;
    }
    public void Abort(global::System.String p0)
    {
    }
    public global::System.Threading.Tasks.Task StartQueueAsync()
    {
        return default;
    }
    public global::System.Threading.Tasks.Task StartQueueAsync<T>(global::System.IProgress<T> p0, global::System.Threading.CancellationToken p1)
    {
        return default;
    }
    public global::System.Threading.Tasks.Task StopQueueAsync()
    {
        return default;
    }
    public void PauseQueue()
    {
    }
    public void ResumeQueue()
    {
    }
}

public sealed class StubAccessControlService : global::Galaxy.Platform.IAccessControl.IAccessControlService
{
    public global::Galaxy.Data.Management.LicenseConfiguration LicenseConfiguration { get; }
    public global::Galaxy.Data.Management.UserConfiguration UserConfiguration { get; }
    public global::Galaxy.Data.Management.UserInfo CurrentUser { get; }
    public global::System.Boolean IsSuperUser { get; }
    public global::System.Boolean SignIn()
    {
        return default;
    }
    public void SignOut()
    {
    }
    public global::System.Threading.Tasks.Task<global::System.Boolean> SignInAsync()
    {
        return default;
    }
    public global::System.Threading.Tasks.Task SignOutAsync()
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::Galaxy.Data.Management.FeatureInfo> GetRegisteredFeatures()
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::Galaxy.Data.Management.FeatureInfo> GetRegisteredFeatures(global::System.Collections.Generic.List<global::System.Int64> p0)
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::Galaxy.Data.Management.FeatureInfo> GetGrantedFeaturesForUsers()
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::Galaxy.Data.Management.FeatureInfo> GetGrantedFeaturesForGlobal()
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::Galaxy.Data.Management.FeatureInfo> GetGrantedFeaturesForCurrentUser()
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::System.String> GetGrantedMarkers()
    {
        return default;
    }
    public global::System.Collections.Generic.List<global::System.String> GetGrants()
    {
        return default;
    }
    public void ImportLicenses(global::System.String p0)
    {
    }
    public void ImportUsers(global::System.String p0)
    {
    }
    public void Load()
    {
    }
    public void Save()
    {
    }
    public global::System.Boolean RequestFeatureAccess(global::System.Int64 p0)
    {
        return default;
    }
    public global::System.Boolean RequestFeatureAccess(global::System.String p0)
    {
        return default;
    }
    public void SignInSilently(global::System.Int64 p0, global::System.String p1)
    {
    }
}

