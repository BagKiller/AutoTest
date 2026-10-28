using Galaxy.Data.Management;
using Galaxy.Platform.IAccessControl;

namespace Vega.AutoTest.CommitProbe;

/// <summary>Test-host only: no user grants. Model tokens come from the real component config.
/// Authentication is intentionally unsupported and this type is never loaded by Vega.exe.</summary>
public sealed class ModelMarkersService : IAccessControlService
{
    public LicenseConfiguration LicenseConfiguration => null!;
    public UserConfiguration UserConfiguration => null!;
    public UserInfo CurrentUser => null!;
    public bool IsSuperUser => false;
    public bool SignIn() => throw new NotSupportedException();
    public void SignOut() => throw new NotSupportedException();
    public Task<bool> SignInAsync() => throw new NotSupportedException();
    public Task SignOutAsync() => throw new NotSupportedException();
    public List<FeatureInfo> GetRegisteredFeatures() => [];
    public List<FeatureInfo> GetRegisteredFeatures(List<long> ids) => [];
    public List<FeatureInfo> GetGrantedFeaturesForUsers() => [];
    public List<FeatureInfo> GetGrantedFeaturesForGlobal() => [];
    public List<FeatureInfo> GetGrantedFeaturesForCurrentUser() => [];
    public List<string> GetGrantedMarkers() => [];
    public List<string> GetGrants() => [];
    public void ImportLicenses(string path) => throw new NotSupportedException();
    public void ImportUsers(string path) => throw new NotSupportedException();
    public void Load() { }
    public void Save() => throw new NotSupportedException();
    public bool RequestFeatureAccess(long id) => false;
    public bool RequestFeatureAccess(string marker) => false;
    public void SignInSilently(long id, string password) => throw new NotSupportedException();
}
