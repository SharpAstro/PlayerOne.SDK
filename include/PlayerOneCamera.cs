using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using TianWen.DAL;

namespace PlayerOne.SDK;

/// <summary>
/// Player One camera API, written against <c>include/PlayerOneCamera.h</c> rather than adapted from
/// the vendor's own C# file, the same way <c>ZWOptical.SDK/include/ASICamera2.cs</c> and
/// <c>QHYCCD.SDK/include/QHYCamera.cs</c> are. <c>reference/README.md</c> records why, and what is
/// wrong with the vendor file.
/// </summary>
public static partial class PlayerOneCamera
{
    /// <summary>
    /// Both widths ship as this one name (<c>lib/x86</c> and <c>lib/x64</c>), and the Unix builds
    /// are <c>libPlayerOneCamera.so</c> / <c>.dylib</c>, which the runtime prefixes and suffixes by
    /// itself. So one import per function serves every RID, and the vendor's 32/64 entry point
    /// split is not needed here.
    /// </summary>
    private const string POACameraLib = "PlayerOneCamera";

    /// <summary>
    /// Is the C <c>long</c> 64 bits on this platform?
    /// </summary>
    /// <remarks>
    /// <para>This is NOT the same question as the pointer width, and using <c>nint</c> for a C
    /// <c>long</c> is wrong on the platform this package is most used on. Windows is LLP64, so a C
    /// <c>long</c> is 4 bytes even on x64; the 64-bit Unixes are LP64, where it is 8. The 32-bit
    /// targets (win-x86, linux-x86, linux-arm) are 4 everywhere.</para>
    /// <para>It matters in two places, and they fail differently. In
    /// <see cref="POAConfigValue"/> only the INTERPRETATION of the bytes changes, because the union
    /// is 8 bytes wide on every platform thanks to its <c>double</c>. In
    /// <c>POAGetImageData</c> the ABI itself changes: passing an 8-byte argument where the callee
    /// expects 4 consumes two slots on a 32-bit target and corrupts the argument after it, which is
    /// the timeout. Hence the two entry points below rather than one clever type.</para>
    /// </remarks>
    private static readonly bool CLongIs64Bit = !OperatingSystem.IsWindows() && IntPtr.Size == 8;

    public enum POABool
    {
        POA_FALSE = 0,
        POA_TRUE
    }

    public enum POABayerPattern
    {
        POA_BAYER_RG = 0,
        POA_BAYER_BG,
        POA_BAYER_GR,
        POA_BAYER_GB,
        POA_BAYER_MONO = -1
    }

    public enum POAImgFormat
    {
        POA_RAW8 = 0,
        POA_RAW16,
        POA_RGB24,
        POA_MONO8,
        POA_END = -1
    }

    public enum POAErrors
    {
        POA_OK = 0,
        POA_ERROR_INVALID_INDEX,
        POA_ERROR_INVALID_ID,
        POA_ERROR_INVALID_CONFIG,
        POA_ERROR_INVALID_ARGU,
        POA_ERROR_NOT_OPENED,
        POA_ERROR_DEVICE_NOT_FOUND,
        POA_ERROR_OUT_OF_LIMIT,
        POA_ERROR_EXPOSURE_FAILED,
        POA_ERROR_TIMEOUT,
        POA_ERROR_SIZE_LESS,
        POA_ERROR_EXPOSING,
        POA_ERROR_POINTER,
        POA_ERROR_CONF_CANNOT_WRITE,
        POA_ERROR_CONF_CANNOT_READ,
        POA_ERROR_ACCESS_DENIED,
        POA_ERROR_OPERATION_FAILED,
        POA_ERROR_MEMORY_FAILED
    }

    public enum POACameraState
    {
        STATE_CLOSED = 0,
        STATE_OPENED,
        STATE_EXPOSING
    }

    public enum POAValueType
    {
        VAL_INT = 0,
        VAL_FLOAT,
        VAL_BOOL
    }

    public enum POAConfig
    {
        POA_EXPOSURE = 0,
        POA_GAIN,
        POA_HARDWARE_BIN,
        POA_TEMPERATURE,
        POA_WB_R,
        POA_WB_G,
        POA_WB_B,
        POA_OFFSET,
        POA_AUTOEXPO_MAX_GAIN,
        POA_AUTOEXPO_MAX_EXPOSURE,
        POA_AUTOEXPO_BRIGHTNESS,
        POA_GUIDE_NORTH,
        POA_GUIDE_SOUTH,
        POA_GUIDE_EAST,
        POA_GUIDE_WEST,
        POA_EGAIN,
        POA_COOLER_POWER,
        POA_TARGET_TEMP,
        POA_COOLER,
        POA_HEATER,
        POA_HEATER_POWER,
        POA_FAN_POWER,
        POA_FLIP_NONE,
        POA_FLIP_HORI,
        POA_FLIP_VERT,
        POA_FLIP_BOTH,
        POA_FRAME_LIMIT,
        POA_HQI,
        POA_USB_BANDWIDTH_LIMIT,
        POA_PIXEL_BIN_SUM,
        POA_MONO_BIN,
        POA_EXP
    }

    /// <summary>
    /// The <c>POAConfigValue</c> union: a C <c>long</c>, a <c>double</c> and a <c>POABool</c> at the
    /// same address, 8 bytes wide on every platform because of the <c>double</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>The integer view is deliberately a 64-bit field, and the vendor's is not.</b> Their
    /// binding declares <c>int intValue</c> at offset 0, which is right on Windows and silently
    /// wrong on a 64-bit Unix for any NEGATIVE value: only the low four bytes get written, the high
    /// four stay zero, and the callee reads the result as a large positive number. -1200 becomes
    /// 4294966096. That is not a hypothetical corner, it is exactly the white balance range this
    /// camera family uses, so the first control it breaks is the one
    /// <see cref="ICMOSNativeInterface.TryGetWhiteBalanceRange"/> exists for.</para>
    /// <para>Writing a sign-extended 64-bit value is correct on BOTH: a 64-bit callee reads all
    /// eight bytes and gets the value, while a 32-bit-long callee reads the low four, which for a
    /// sign-extended value are the same bits as the 32-bit form. Reading back is correct on both
    /// too, by taking the low 32 bits: a Windows callee writes only those four bytes, so the high
    /// four must not be read, and every value this API carries fits in 32 bits anyway (the largest
    /// is an exposure of 2,000,000,000 us). Hence <see cref="IntValue"/> rather than raw field
    /// access at either call site.</para>
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct POAConfigValue
    {
        [FieldOffset(0)]
        private long _intValue;

        [FieldOffset(0)]
        private double _floatValue;

        /// <summary>
        /// The integer view, read as the low 32 bits and written sign-extended. See the type's
        /// remarks for why neither half is incidental.
        /// </summary>
        public int IntValue
        {
            readonly get => unchecked((int)_intValue);
            set => _intValue = value;
        }

        public double FloatValue
        {
            readonly get => _floatValue;
            set => _floatValue = value;
        }

        public bool BoolValue
        {
            readonly get => unchecked((int)_intValue) != 0;
            set => _intValue = value ? 1 : 0;
        }

        public static POAConfigValue FromInt(int value) => new POAConfigValue { IntValue = value };

        public static POAConfigValue FromDouble(double value) => new POAConfigValue { FloatValue = value };

        public static POAConfigValue FromBool(bool value) => new POAConfigValue { BoolValue = value };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POAConfigAttributes
    {
        public POABool IsSupportAuto;
        public POABool IsWritable;
        public POABool IsReadable;
        public POAConfig ConfigID;
        public POAValueType ValueType;
        public POAConfigValue MaxValue;
        public POAConfigValue MinValue;
        public POAConfigValue DefaultValue;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
        private byte[] _confName;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 128)]
        private byte[] _description;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
        private byte[] _reserved;

        public readonly string ConfName => AsciiOf(_confName);

        public readonly string Description => AsciiOf(_description);
    }

    /// <summary>
    /// <c>POACameraProperties</c>, implementing the DAL camera contract directly rather than through
    /// a wrapper layer.
    /// </summary>
    /// <remarks>
    /// Field order and types mirror the C struct exactly and must not be reordered.
    /// <see cref="LayoutKind.Sequential"/> with default packing reproduces the compiler's own
    /// padding, including the four bytes before <c>pixelSize</c> that its 8-byte alignment forces.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct POACameraProperties : ICMOSNativeInterface
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 256)]
        private byte[] _cameraModelName;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 16)]
        private byte[] _userCustomID;

        private int _cameraID;
        private int _maxWidth;
        private int _maxHeight;
        private int _bitDepth;
        private POABool _isColorCamera;
        private POABool _isHasST4Port;
        private POABool _isHasCooler;
        private POABool _isUSB3Speed;
        private POABayerPattern _bayerPattern;
        private double _pixelSize;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
        private byte[] _sn;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
        private byte[] _sensorModelName;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 256)]
        private byte[] _localPath;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        private int[] _bins;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        private POAImgFormat[] _imgFormats;

        private POABool _isSupportHardBin;
        private int _pID;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 248)]
        private byte[] _reserved;

        // ---- Identity -----------------------------------------------------------------------

        public readonly int ID => _cameraID;

        public readonly string Name => AsciiOf(_cameraModelName);

        /// <summary>
        /// The camera name with the user's custom id appended, as the vendor displays it
        /// (<c>Mars-C [Juno]</c>), or just the name when none is set.
        /// </summary>
        public readonly string CustomId
        {
            get
            {
                var custom = AsciiOf(_userCustomID);
                return custom.Length == 0 ? Name : $"{Name} [{custom}]";
            }
        }

        /// <summary>
        /// The factory serial, or null when the body reports none.
        /// </summary>
        /// <remarks>
        /// Player One writes a printable ASCII serial straight into the properties struct
        /// (<c>CAMP3103A85002109000</c> on the bench Uranus-C), so unlike ZWO there is no separate
        /// call and no hex decode. An empty field is surfaced as null rather than as an empty
        /// string, so a caller cannot mistake it for an identity.
        /// </remarks>
        public readonly string? SerialNumber
        {
            get
            {
                var sn = AsciiOf(_sn);
                return sn.Length == 0 ? null : sn;
            }
        }

        public readonly bool IsUSB3Device => _isUSB3Speed is POABool.POA_TRUE;

        /// <summary>
        /// The sensor die, as the CAMERA reports it.
        /// </summary>
        /// <remarks>
        /// Player One states this in the properties struct (<c>IMX585</c>), so this is read rather
        /// than inferred. ZWO has no such field and its binding recovers the die from the product
        /// name through <c>SensorModelNames</c>, which is a lookup that can be wrong or missing; a
        /// vendor that simply says so is preferable, and a QE curve keys on the die.
        /// </remarks>
        public readonly string? SensorModel
        {
            get
            {
                var model = AsciiOf(_sensorModelName);
                return model.Length == 0 ? null : model;
            }
        }

        public readonly bool Open()
            => POAOpenCamera(_cameraID) is POAErrors.POA_OK && POAInitCamera(_cameraID) is POAErrors.POA_OK;

        public readonly bool Close() => POACloseCamera(_cameraID) is POAErrors.POA_OK;

        // ---- What the sensor is -------------------------------------------------------------

        public readonly int MaxHeight => _maxHeight;

        public readonly int MaxWidth => _maxWidth;

        public readonly int BitDepth => _bitDepth;

        public readonly double PixelSize => _pixelSize;

        public readonly BayerPattern BayerPattern => _isColorCamera is POABool.POA_TRUE
            ? _bayerPattern switch
            {
                POABayerPattern.POA_BAYER_RG => TianWen.DAL.BayerPattern.RGGB,
                POABayerPattern.POA_BAYER_BG => TianWen.DAL.BayerPattern.BGGR,
                POABayerPattern.POA_BAYER_GR => TianWen.DAL.BayerPattern.GRBG,
                POABayerPattern.POA_BAYER_GB => TianWen.DAL.BayerPattern.GBRG,
                _ => throw new NotSupportedException($"Unsupported Bayer pattern: {_bayerPattern}")
            }
            : TianWen.DAL.BayerPattern.Monochrome;

        /// <summary>Supported binnings, the array being zero-terminated.</summary>
        public readonly IReadOnlyList<int> SupportedBins
        {
            get
            {
                var list = new List<int>(_bins.Length);
                foreach (var bin in _bins)
                {
                    if (bin == 0)
                    {
                        break;
                    }

                    list.Add(bin);
                }

                return list;
            }
        }

        /// <summary>Supported pixel formats, the array being <c>POA_END</c>-terminated.</summary>
        public readonly IReadOnlyList<PixelDataFormat> SupportedPixelDataFormats
        {
            get
            {
                var list = new List<PixelDataFormat>(_imgFormats.Length);
                foreach (var format in _imgFormats)
                {
                    if (format is POAImgFormat.POA_END)
                    {
                        break;
                    }

                    list.Add(format switch
                    {
                        POAImgFormat.POA_RAW8 => PixelDataFormat.RAW8,
                        POAImgFormat.POA_RAW16 => PixelDataFormat.RAW16,
                        POAImgFormat.POA_RGB24 => PixelDataFormat.RGB24,
                        POAImgFormat.POA_MONO8 => PixelDataFormat.Y8,
                        _ => throw new NotSupportedException($"Unsupported image format: {format}")
                    });
                }

                return list;
            }
        }

        // ---- What the body can do -----------------------------------------------------------

        /// <summary>
        /// False: the properties struct declares no trigger capability, and a capability that
        /// cannot be asked about is one we do not claim.
        /// </summary>
        public readonly bool IsTriggerCamera => false;

        /// <summary>False: no Player One body in this SDK has a mechanical shutter.</summary>
        public readonly bool HasMechanicalShutter => false;

        public readonly bool HasCooler => _isHasCooler is POABool.POA_TRUE;

        public readonly bool HasST4Port => _isHasST4Port is POABool.POA_TRUE;

        /// <summary>
        /// True for a colour body: Player One exposes <c>POA_WB_G</c> as a control of its own, so a
        /// caller writing a neutral balance has three channels to write, not two.
        /// </summary>
        public readonly bool HasThreeChannelWhiteBalance => _isColorCamera is POABool.POA_TRUE;

        // ---- Controls -----------------------------------------------------------------------

        /// <summary>
        /// Electrons per ADU at the CURRENT gain.
        /// </summary>
        /// <remarks>
        /// A read of <c>POA_EGAIN</c>, not a constant: the header states it changes with gain, so a
        /// value cached at connect describes only the gain it was read at. Zero when the camera is
        /// not open, which is the same answer the caller would get from a closed ZWO body.
        /// </remarks>
        public readonly double ElectronPerADU
            => POAGetConfig(_cameraID, POAConfig.POA_EGAIN, out var value, out _) is POAErrors.POA_OK
                ? value.FloatValue
                : 0d;

        public readonly bool TryGetControlRange(CMOSControlType ctrlType, out int min, out int max)
        {
            min = max = 0;

            if (!DALControlTypeToPOA(ctrlType, out var config)
                || POAGetConfigAttributesByConfigID(_cameraID, config, out var attributes) is not POAErrors.POA_OK)
            {
                return false;
            }

            (min, max) = attributes.ValueType switch
            {
                POAValueType.VAL_FLOAT when ctrlType is CMOSControlType.TemperatureDeci
                    => ((int)Math.Round(attributes.MinValue.FloatValue * 10),
                        (int)Math.Round(attributes.MaxValue.FloatValue * 10)),
                POAValueType.VAL_FLOAT
                    => ((int)Math.Round(attributes.MinValue.FloatValue),
                        (int)Math.Round(attributes.MaxValue.FloatValue)),
                _ => (attributes.MinValue.IntValue, attributes.MaxValue.IntValue)
            };

            return true;
        }

        /// <summary>
        /// The white balance scale, read from the camera.
        /// </summary>
        /// <remarks>
        /// <para>Bounds come from <c>POA_WB_R</c>'s own attributes rather than a literal, so a body
        /// with a different range answers for itself. Neutral is 0 because these controls are
        /// SIGNED offsets about no-gain, which is a property of the control's definition and not of
        /// one model.</para>
        /// <para>Checked on hardware before being relied on, a connected Uranus-C
        /// (sn CAMP3103A85002109000) reporting range [-1200, 1200] with both the SDK default and
        /// the arithmetic midpoint at 0, and an archive frame captured at 0 measuring a red gain of
        /// exactly 1.000 in its pixels. The vendor default is NOT assumed to be neutral in general,
        /// since a default may be a pleasant daylight balance; here all three agree.</para>
        /// </remarks>
        public readonly bool TryGetWhiteBalanceRange(out int min, out int max, out int neutral)
        {
            min = max = neutral = 0;

            if (_isColorCamera is not POABool.POA_TRUE
                || POAGetConfigAttributesByConfigID(_cameraID, POAConfig.POA_WB_R, out var attributes) is not POAErrors.POA_OK)
            {
                return false;
            }

            min = attributes.MinValue.IntValue;
            max = attributes.MaxValue.IntValue;
            neutral = 0;
            return true;
        }

        public readonly CMOSErrorCode GetControlValue(CMOSControlType controlType, out int value, out bool isAuto)
        {
            value = 0;
            isAuto = false;

            if (!DALControlTypeToPOA(controlType, out var config))
            {
                return CMOSErrorCode.InvalidControlType;
            }

            var error = POAGetConfig(_cameraID, config, out var raw, out var auto);
            if (error is not POAErrors.POA_OK)
            {
                return ToDALError(error);
            }

            isAuto = auto is POABool.POA_TRUE;
            value = ValueTypeOf(config) switch
            {
                // TemperatureDeci is the DAL's tenths convention over a control the SDK reports in
                // whole degrees as a double.
                POAValueType.VAL_FLOAT when controlType is CMOSControlType.TemperatureDeci
                    => (int)Math.Round(raw.FloatValue * 10),
                POAValueType.VAL_FLOAT => (int)Math.Round(raw.FloatValue),
                _ => raw.IntValue
            };

            return CMOSErrorCode.Success;
        }

        public readonly CMOSErrorCode SetControlValue(CMOSControlType controlType, int value, bool isAuto = false)
        {
            if (!DALControlTypeToPOA(controlType, out var config))
            {
                return CMOSErrorCode.InvalidControlType;
            }

            var raw = ValueTypeOf(config) switch
            {
                POAValueType.VAL_FLOAT when controlType is CMOSControlType.TemperatureDeci
                    => POAConfigValue.FromDouble(value / 10d),
                POAValueType.VAL_FLOAT => POAConfigValue.FromDouble(value),
                POAValueType.VAL_BOOL => POAConfigValue.FromBool(value != 0),
                _ => POAConfigValue.FromInt(value)
            };

            return ToDALError(POASetConfig(
                _cameraID,
                config,
                raw,
                isAuto ? POABool.POA_TRUE : POABool.POA_FALSE));
        }

        // ---- Guiding ------------------------------------------------------------------------

        /// <summary>
        /// False: ST4 here is a pair of level controls with no duration argument, so the caller
        /// keeps its own timer and calls <see cref="PulseGuideOff"/>.
        /// </summary>
        public readonly bool CanPulseGuideForDuration => false;

        public readonly CMOSErrorCode PulseGuideOn(GuideDirection direction)
            => ToDALError(POASetConfig(_cameraID, GuideConfigOf(direction), POAConfigValue.FromBool(true), POABool.POA_FALSE));

        public readonly CMOSErrorCode PulseGuideOff(GuideDirection direction)
            => ToDALError(POASetConfig(_cameraID, GuideConfigOf(direction), POAConfigValue.FromBool(false), POABool.POA_FALSE));

        // ---- Exposure -----------------------------------------------------------------------

        /// <summary>
        /// Starts a single-frame exposure.
        /// </summary>
        /// <remarks>
        /// <see cref="StartDarkExposure"/> is the same call. The distinction the DAL draws is about
        /// a MECHANICAL SHUTTER, and this body has none (<see cref="HasMechanicalShutter"/>), so a
        /// dark is made by capping the telescope, not by the driver. Returning an error instead
        /// would be wrong: the exposure is perfectly valid, it simply cannot be darkened from here.
        /// </remarks>
        public readonly CMOSErrorCode StartLightExposure()
            => ToDALError(POAStartExposure(_cameraID, POABool.POA_TRUE));

        /// <inheritdoc cref="StartLightExposure"/>
        public readonly CMOSErrorCode StartDarkExposure()
            => ToDALError(POAStartExposure(_cameraID, POABool.POA_TRUE));

        public readonly CMOSErrorCode StopExposure() => ToDALError(POAStopExposure(_cameraID));

        /// <summary>
        /// Maps camera state plus frame readiness onto the DAL's four-state view.
        /// </summary>
        /// <remarks>
        /// Two calls are needed because the SDK splits the question: <c>POAGetCameraState</c> says
        /// whether it is still exposing, and only <c>POAImageReady</c> distinguishes a finished
        /// exposure with a frame waiting from an idle camera, both of which report
        /// <c>STATE_OPENED</c>. A failed exposure is not a state here; it surfaces as an error from
        /// the data read, so it is not invented at this level.
        /// </remarks>
        public readonly CMOSErrorCode GetExposureStatus(out ExposureStatus exposureStatus)
        {
            exposureStatus = ExposureStatus.Idle;

            var error = POAGetCameraState(_cameraID, out var state);
            if (error is not POAErrors.POA_OK)
            {
                return ToDALError(error);
            }

            if (state is POACameraState.STATE_EXPOSING)
            {
                exposureStatus = ExposureStatus.Working;
                return CMOSErrorCode.Success;
            }

            error = POAImageReady(_cameraID, out var ready);
            if (error is not POAErrors.POA_OK)
            {
                return ToDALError(error);
            }

            exposureStatus = ready is POABool.POA_TRUE ? ExposureStatus.Success : ExposureStatus.Idle;
            return CMOSErrorCode.Success;
        }

        // ---- Region of interest -------------------------------------------------------------

        public readonly CMOSErrorCode GetStartPosition(out int startX, out int startY)
            => ToDALError(POAGetImageStartPos(_cameraID, out startX, out startY));

        public readonly CMOSErrorCode SetStartPosition(int startX, int startY)
            => ToDALError(POASetImageStartPos(_cameraID, startX, startY));

        public readonly CMOSErrorCode GetROIFormat(out int width, out int height, out int bin, out PixelDataFormat pixelDataFormat)
        {
            width = height = bin = 0;
            pixelDataFormat = PixelDataFormat.RAW8;

            var error = POAGetImageSize(_cameraID, out width, out height);
            if (error is not POAErrors.POA_OK)
            {
                return ToDALError(error);
            }

            error = POAGetImageBin(_cameraID, out bin);
            if (error is not POAErrors.POA_OK)
            {
                return ToDALError(error);
            }

            error = POAGetImageFormat(_cameraID, out var format);
            if (error is not POAErrors.POA_OK)
            {
                return ToDALError(error);
            }

            pixelDataFormat = format switch
            {
                POAImgFormat.POA_RAW8 => PixelDataFormat.RAW8,
                POAImgFormat.POA_RAW16 => PixelDataFormat.RAW16,
                POAImgFormat.POA_RGB24 => PixelDataFormat.RGB24,
                POAImgFormat.POA_MONO8 => PixelDataFormat.Y8,
                _ => PixelDataFormat.RAW8
            };

            return CMOSErrorCode.Success;
        }

        /// <summary>
        /// Sets the region of interest.
        /// </summary>
        /// <remarks>
        /// Order matters and is not arbitrary: the bin is set FIRST, because the SDK expresses size
        /// in BINNED pixels, so a size written before a bin change is reinterpreted by it. The
        /// header also requires width to be a multiple of 4 and height a multiple of 2; a caller
        /// violating that gets <c>POA_ERROR_INVALID_ARGU</c> back rather than a silently adjusted
        /// frame, which is left to surface rather than rounded here.
        /// </remarks>
        public readonly CMOSErrorCode SetROIFormat(int width, int height, int bin, PixelDataFormat pixelDataFormat)
        {
            var error = POASetImageBin(_cameraID, bin);
            if (error is not POAErrors.POA_OK)
            {
                return ToDALError(error);
            }

            error = POASetImageSize(_cameraID, width, height);
            if (error is not POAErrors.POA_OK)
            {
                return ToDALError(error);
            }

            var format = pixelDataFormat switch
            {
                PixelDataFormat.RAW8 => POAImgFormat.POA_RAW8,
                PixelDataFormat.RAW16 => POAImgFormat.POA_RAW16,
                PixelDataFormat.RGB24 => POAImgFormat.POA_RGB24,
                PixelDataFormat.Y8 => POAImgFormat.POA_MONO8,
                _ => POAImgFormat.POA_END
            };

            return format is POAImgFormat.POA_END
                ? CMOSErrorCode.InvalidImageFormat
                : ToDALError(POASetImageFormat(_cameraID, format));
        }

        // ---- The frame ----------------------------------------------------------------------

        /// <summary>
        /// Copies the finished frame out.
        /// </summary>
        /// <remarks>
        /// The buffer size argument is a C <c>long</c>, whose width differs by platform, so the two
        /// entry points are picked between here rather than one being cast. See
        /// <see cref="CLongIs64Bit"/> for why this one cannot be <c>nint</c>.
        /// </remarks>
        public readonly CMOSErrorCode GetDataAfterExposure(IntPtr buffer, int bufferSize)
            => ToDALError(CLongIs64Bit
                ? POAGetImageData64(_cameraID, buffer, bufferSize, ImageDataTimeoutMs)
                : POAGetImageData32(_cameraID, buffer, bufferSize, ImageDataTimeoutMs));

        /// <summary>
        /// How long the frame read may block. The exposure is already complete by the time a caller
        /// reaches here (it asks <see cref="GetExposureStatus"/> first), so this bounds the USB
        /// transfer alone, not the exposure.
        /// </summary>
        private const int ImageDataTimeoutMs = 5_000;
    }

    // ---- Mapping helpers --------------------------------------------------------------------

    private static string AsciiOf(byte[]? raw)
        => raw is null ? string.Empty : Encoding.ASCII.GetString(raw).TrimEnd('\0').Trim();

    private static POAConfig GuideConfigOf(GuideDirection direction) => direction switch
    {
        GuideDirection.North => POAConfig.POA_GUIDE_NORTH,
        GuideDirection.South => POAConfig.POA_GUIDE_SOUTH,
        GuideDirection.East => POAConfig.POA_GUIDE_EAST,
        GuideDirection.West => POAConfig.POA_GUIDE_WEST,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown guide direction")
    };

    /// <summary>
    /// The value type of a config, asked of the SDK rather than tabulated here, so a future SDK
    /// that changes one does not leave a stale copy behind. Falls back to
    /// <see cref="POAValueType.VAL_INT"/> only when the call itself fails.
    /// </summary>
    private static POAValueType ValueTypeOf(POAConfig config)
        => POAGetConfigValueType(config, out var valueType) is POAErrors.POA_OK ? valueType : POAValueType.VAL_INT;

    /// <summary>
    /// Maps a DAL control onto its Player One config, returning false for one this vendor does not
    /// have.
    /// </summary>
    /// <remarks>
    /// <para>False is a real answer, not a failure: the DAL enum is the union of what several
    /// vendors offer, so <c>Gamma</c>, <c>Overclock</c>, <c>HighSpeedMode</c>, <c>PatternAdjust</c>,
    /// <c>Humidity</c> and <c>EnableDDR</c> have no Player One equivalent and must refuse rather
    /// than be mapped onto something adjacent.</para>
    /// <para>Two mappings are worth stating because they are not name-for-name.
    /// <c>Brightness</c> is the black level, which this vendor calls <c>POA_OFFSET</c>, matching
    /// how ZWO's <c>ASI_BRIGHTNESS</c> is already treated. <c>FanOn</c> maps to
    /// <c>POA_FAN_POWER</c>, a percentage rather than a switch, so a caller writing 1 for "on" gets
    /// 1 percent; that is a caller-visible difference the DAL has no vocabulary for yet, and it is
    /// mapped rather than refused because a cooled body's fan does need to be reachable.</para>
    /// <para><c>Flip</c> is deliberately NOT mapped. Player One spreads it over four separate
    /// configs whose value argument is ignored, so the value the DAL carries has no meaning here
    /// and a partial mapping would silently discard it.</para>
    /// </remarks>
    private static bool DALControlTypeToPOA(CMOSControlType controlType, out POAConfig config)
    {
        switch (controlType)
        {
            case CMOSControlType.Gain: config = POAConfig.POA_GAIN; return true;
            case CMOSControlType.Exposure: config = POAConfig.POA_EXPOSURE; return true;
            case CMOSControlType.WB_R: config = POAConfig.POA_WB_R; return true;
            case CMOSControlType.WB_G: config = POAConfig.POA_WB_G; return true;
            case CMOSControlType.WB_B: config = POAConfig.POA_WB_B; return true;
            case CMOSControlType.Brightness: config = POAConfig.POA_OFFSET; return true;
            case CMOSControlType.BandwidthOverload: config = POAConfig.POA_USB_BANDWIDTH_LIMIT; return true;
            case CMOSControlType.TemperatureDeci: config = POAConfig.POA_TEMPERATURE; return true;
            case CMOSControlType.AutoMaxGain: config = POAConfig.POA_AUTOEXPO_MAX_GAIN; return true;
            case CMOSControlType.AutoMaxExposure: config = POAConfig.POA_AUTOEXPO_MAX_EXPOSURE; return true;
            case CMOSControlType.AutoMaxBrightness: config = POAConfig.POA_AUTOEXPO_BRIGHTNESS; return true;
            case CMOSControlType.HardwareBin: config = POAConfig.POA_HARDWARE_BIN; return true;
            case CMOSControlType.CoolerPowerPercent: config = POAConfig.POA_COOLER_POWER; return true;
            case CMOSControlType.TargetTemperature: config = POAConfig.POA_TARGET_TEMP; return true;
            case CMOSControlType.CoolerOn: config = POAConfig.POA_COOLER; return true;
            case CMOSControlType.MonoBin: config = POAConfig.POA_MONO_BIN; return true;
            case CMOSControlType.FanOn: config = POAConfig.POA_FAN_POWER; return true;
            case CMOSControlType.AntiDewHeater: config = POAConfig.POA_HEATER_POWER; return true;
            default: config = default; return false;
        }
    }

    /// <summary>
    /// Maps a Player One error onto the DAL's code.
    /// </summary>
    /// <remarks>
    /// Several Player One errors have no distinct DAL counterpart and land on
    /// <see cref="CMOSErrorCode.GeneralError"/>. That is deliberate: inventing a closer-sounding
    /// code would assert a cause the SDK did not report.
    /// </remarks>
    private static CMOSErrorCode ToDALError(POAErrors error) => error switch
    {
        POAErrors.POA_OK => CMOSErrorCode.Success,
        POAErrors.POA_ERROR_INVALID_INDEX => CMOSErrorCode.InvalidIndex,
        POAErrors.POA_ERROR_INVALID_ID => CMOSErrorCode.InvalidId,
        POAErrors.POA_ERROR_INVALID_CONFIG => CMOSErrorCode.InvalidControlType,
        POAErrors.POA_ERROR_INVALID_ARGU => CMOSErrorCode.InvalidSize,
        POAErrors.POA_ERROR_NOT_OPENED => CMOSErrorCode.CameraClosed,
        POAErrors.POA_ERROR_DEVICE_NOT_FOUND => CMOSErrorCode.CameraRemoved,
        POAErrors.POA_ERROR_OUT_OF_LIMIT => CMOSErrorCode.OutOfBoundary,
        POAErrors.POA_ERROR_TIMEOUT => CMOSErrorCode.Timeout,
        POAErrors.POA_ERROR_SIZE_LESS => CMOSErrorCode.BufferTooSmall,
        POAErrors.POA_ERROR_EXPOSING => CMOSErrorCode.ExposureInProgress,
        _ => CMOSErrorCode.GeneralError
    };

    // ---- Entry points -----------------------------------------------------------------------

    [LibraryImport(POACameraLib, EntryPoint = "POAGetCameraCount")]
    public static partial int POAGetCameraCount();

    /// <remarks>
    /// Not blittable (the struct carries fixed-size <c>char</c> arrays), so this is a
    /// <c>DllImport</c> rather than a <c>LibraryImport</c>, as the ZWO binding does for the same
    /// reason.
    /// </remarks>
    [DllImport(POACameraLib, EntryPoint = "POAGetCameraProperties")]
    public static extern POAErrors POAGetCameraProperties(int index, out POACameraProperties properties);

    [DllImport(POACameraLib, EntryPoint = "POAGetCameraPropertiesByID")]
    public static extern POAErrors POAGetCameraPropertiesByID(int cameraId, out POACameraProperties properties);

    [LibraryImport(POACameraLib, EntryPoint = "POAOpenCamera")]
    public static partial POAErrors POAOpenCamera(int cameraId);

    [LibraryImport(POACameraLib, EntryPoint = "POAInitCamera")]
    public static partial POAErrors POAInitCamera(int cameraId);

    [LibraryImport(POACameraLib, EntryPoint = "POACloseCamera")]
    public static partial POAErrors POACloseCamera(int cameraId);

    [DllImport(POACameraLib, EntryPoint = "POAGetConfigAttributesByConfigID")]
    public static extern POAErrors POAGetConfigAttributesByConfigID(int cameraId, POAConfig configId, out POAConfigAttributes attributes);

    [LibraryImport(POACameraLib, EntryPoint = "POAGetConfig")]
    public static partial POAErrors POAGetConfig(int cameraId, POAConfig configId, out POAConfigValue value, out POABool isAuto);

    [LibraryImport(POACameraLib, EntryPoint = "POASetConfig")]
    public static partial POAErrors POASetConfig(int cameraId, POAConfig configId, POAConfigValue value, POABool isAuto);

    [LibraryImport(POACameraLib, EntryPoint = "POAGetConfigValueType")]
    public static partial POAErrors POAGetConfigValueType(POAConfig configId, out POAValueType valueType);

    [LibraryImport(POACameraLib, EntryPoint = "POAGetImageStartPos")]
    public static partial POAErrors POAGetImageStartPos(int cameraId, out int startX, out int startY);

    [LibraryImport(POACameraLib, EntryPoint = "POASetImageStartPos")]
    public static partial POAErrors POASetImageStartPos(int cameraId, int startX, int startY);

    [LibraryImport(POACameraLib, EntryPoint = "POAGetImageSize")]
    public static partial POAErrors POAGetImageSize(int cameraId, out int width, out int height);

    [LibraryImport(POACameraLib, EntryPoint = "POASetImageSize")]
    public static partial POAErrors POASetImageSize(int cameraId, int width, int height);

    [LibraryImport(POACameraLib, EntryPoint = "POAGetImageBin")]
    public static partial POAErrors POAGetImageBin(int cameraId, out int bin);

    [LibraryImport(POACameraLib, EntryPoint = "POASetImageBin")]
    public static partial POAErrors POASetImageBin(int cameraId, int bin);

    [LibraryImport(POACameraLib, EntryPoint = "POAGetImageFormat")]
    public static partial POAErrors POAGetImageFormat(int cameraId, out POAImgFormat format);

    [LibraryImport(POACameraLib, EntryPoint = "POASetImageFormat")]
    public static partial POAErrors POASetImageFormat(int cameraId, POAImgFormat format);

    [LibraryImport(POACameraLib, EntryPoint = "POAStartExposure")]
    public static partial POAErrors POAStartExposure(int cameraId, POABool isSingleFrame);

    [LibraryImport(POACameraLib, EntryPoint = "POAStopExposure")]
    public static partial POAErrors POAStopExposure(int cameraId);

    [LibraryImport(POACameraLib, EntryPoint = "POAGetCameraState")]
    public static partial POAErrors POAGetCameraState(int cameraId, out POACameraState state);

    [LibraryImport(POACameraLib, EntryPoint = "POAImageReady")]
    public static partial POAErrors POAImageReady(int cameraId, out POABool isReady);

    /// <summary>
    /// <c>POAGetImageData</c> where the C <c>long</c> buffer size is 32 bits (Windows, and every
    /// 32-bit target).
    /// </summary>
    [LibraryImport(POACameraLib, EntryPoint = "POAGetImageData")]
    private static partial POAErrors POAGetImageData32(int cameraId, IntPtr buffer, int bufferSize, int timeoutMs);

    /// <summary>
    /// <c>POAGetImageData</c> where the C <c>long</c> buffer size is 64 bits (the LP64 Unixes).
    /// </summary>
    /// <remarks>
    /// A separate entry point rather than a cast, because the difference is in the ABI and not just
    /// the value: an 8-byte argument passed where 4 are expected takes two slots on a 32-bit target
    /// and corrupts the timeout that follows it.
    /// </remarks>
    [LibraryImport(POACameraLib, EntryPoint = "POAGetImageData")]
    private static partial POAErrors POAGetImageData64(int cameraId, IntPtr buffer, long bufferSize, int timeoutMs);

    /// <summary>
    /// The SDK's own description of an error code.
    /// </summary>
    /// <remarks>
    /// Returns a pointer to a static string inside the library, so it is never null in practice;
    /// the null-coalesce states that rather than asserting it with a <c>!</c>.
    /// </remarks>
    public static string POAGetErrorStringText(POAErrors error)
        => Marshal.PtrToStringAnsi(POAGetErrorString(error)) ?? error.ToString();

    [LibraryImport(POACameraLib, EntryPoint = "POAGetErrorString")]
    private static partial IntPtr POAGetErrorString(POAErrors error);

    [LibraryImport(POACameraLib, EntryPoint = "POAGetSDKVersion")]
    private static partial IntPtr POAGetSDKVersionPtr();

    /// <summary>The SDK version string, e.g. <c>3.10.1</c>.</summary>
    public static string POAGetSDKVersion() => Marshal.PtrToStringAnsi(POAGetSDKVersionPtr()) ?? string.Empty;
}
