using TianWen.DAL;
using static PlayerOne.SDK.PlayerOneCamera;

namespace PlayerOne.SDK;

/// <summary>
/// Enumerates connected Player One devices, shaped like the ZWO and QHY iterators so a consumer
/// reads one pattern across all three vendors.
/// </summary>
/// <remarks>
/// <para><b>Counting is not merely the loop bound here, it is the enumeration itself.</b>
/// <c>POAGetCameraCount</c> is what makes any camera's properties valid; calling
/// <c>POAGetCameraProperties</c> without it first hands back a ZEROED struct and reports success, so
/// every later call runs against camera id 0 and the failure looks like data rather than an error.
/// Measured on the bench: a probe that skipped the count read a sensor temperature of 0.00 C from a
/// camera sitting at 21.30 C.</para>
/// <para><see cref="NativeDeviceIteratorBase{TDeviceInfo}"/> calls <see cref="DeviceCount"/> before
/// <see cref="GetDeviceInfo"/> by construction, so the ordering holds for free and no caller has to
/// know this. It is written down because the obvious "optimisation" of caching a count, or of
/// fetching properties for a known index without counting first, reintroduces it silently.</para>
/// </remarks>
public class DeviceIterator<TDeviceInfo> : NativeDeviceIteratorBase<TDeviceInfo>
    where TDeviceInfo : struct, INativeDeviceInfo
{
    protected override int DeviceCount()
        => typeof(TDeviceInfo) == typeof(POACameraProperties) ? POAGetCameraCount() : 0;

    protected override TDeviceInfo? GetDeviceInfo(int index)
    {
        if (typeof(TDeviceInfo) == typeof(POACameraProperties)
            && POAGetCameraProperties(index, out var properties) is POAErrors.POA_OK)
        {
            return (TDeviceInfo)(INativeDeviceInfo)properties;
        }

        return null;
    }
}
