using VoxelTycoon;

namespace BlueprintMod;

/// <summary>Safe asset lookups for asset names that come from blueprint files.</summary>
internal static class AssetLibraryExtensions
{
    /// <summary>
    /// Finds an asset by name and checks its type. <c>AssetLibrary.TryGet&lt;T&gt;</c> casts the asset to <c>T</c>
    /// and throws when the name belongs to an asset of another type, so the type is checked here instead.
    /// </summary>
    public static bool TryGetAsset<T>(this AssetLibrary assets, string assetUri, out int assetId, out T asset)
        where T : class
    {
        asset = null;
        if (!assets.TryGetAssetId(assetUri, out assetId) || !assets.TryGet(assetId, out object candidate))
            return false;

        asset = candidate as T;
        return asset != null;
    }
}
