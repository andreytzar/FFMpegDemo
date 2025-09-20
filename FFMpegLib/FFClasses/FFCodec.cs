using FFmpeg.AutoGen;
using FFMpegLib.Helpers;
using System.Windows.Media.Media3D;


namespace FFMpegLib.FFClasses
{
    public unsafe class FFCodec : IDisposable
    {
       
        public StreamType StreamType { get; private set; } = StreamType.UNKNOWN;
        public CodecID CodecID { get; private set; } = CodecID.UNKNOWN;
        public string Codec_tag { get; private set; } = string.Empty;
        public VideoParam? VideoParam { get; private set; }
        public AudioParam? AudioParam { get; private set; }
        public EventHandler<string>? OnError;
        internal AVCodecContext* CodecContext { get =>_codecctx;}
        public bool CodecInited { get; private set; }=false;
        readonly object _lock = new object();

        AVCodecParameters* _codecpar;
        AVCodec* _codec = null;
        AVCodecContext* _codecctx = null;

        public FFCodec(AVCodecParameters* codecpar)
        {
            _codecpar = codecpar;
            StreamType = (StreamType)_codecpar->codec_type;
            CodecID = (CodecID)_codecpar->codec_id;
            Codec_tag = ffUtils.UIntToString(_codecpar->codec_tag);
            InitParam();
        }


        void InitParam()
        {
            lock (_lock)
            {
                switch (StreamType)
                {
                    case StreamType.UNKNOWN: break;
                    case StreamType.AUDIO:
                        AudioParam = new AudioParam(_codecpar);
                        InitCodec();
                        break;
                    case StreamType.VIDEO:
                        VideoParam = new VideoParam(_codecpar);
                        InitCodec();
                        break;
                }
            }
        }

        void InitCodec()
        {
            if (StreamType != StreamType.AUDIO && StreamType != StreamType.VIDEO) return;
            var codec = ffmpeg.avcodec_find_decoder(_codecpar->codec_id);
            if (codec != null)
            {
                var ctx = ffmpeg.avcodec_alloc_context3(codec);
                if (ctx != null)
                {
                    int err = ffmpeg.avcodec_parameters_to_context(ctx, _codecpar);
                    if (err<0)
                    {
                        OnError?.Invoke(this, $"codec error {err.av_errorToString()}");
                        ffmpeg.avcodec_free_context(&ctx);
                        return;
                    }
                    err = ffmpeg.avcodec_open2(ctx, codec, null);
                    if (err < 0)
                    {
                        OnError?.Invoke(this, $"codec error {err.av_errorToString()}");
                        ffmpeg.avcodec_free_context(&ctx);
                        return;
                    }
                    _codec = codec;
                    _codecctx = ctx;
                    CodecInited = true;
                } else OnError?.Invoke(this, $"Could not create CodecConext");
            }
            else OnError?.Invoke(this, $"No codec found");
        }

        void Close()
        {
            lock (_lock)
            {
                if (_codecctx != null)
                {
                    AVCodecContext* ctx = _codecctx;
                    ffmpeg.avcodec_free_context(&ctx);
                    _codecctx = null;
                }
                _codec = null;
                VideoParam = null;
                AudioParam = null;
                CodecInited= false;
            }
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            switch (StreamType)
            {
                case StreamType.UNKNOWN: break;
                case StreamType.AUDIO:
                    return $"{Codec_tag} {CodecID} {AudioParam}";
                case StreamType.VIDEO:
                    return $"{Codec_tag} {CodecID} {VideoParam}";
            }
            return $"{StreamType}";
        }
    }
    public unsafe class VideoParam
    {
        internal AVPixelFormat PixelFormat { get; private set; }
        public long Bit_rate { get; private set; }
        /// <summary>Video only. The dimensions of the video frame in pixels.</summary>
        public int Width { get; private set; }
        /// <summary>Video only. The dimensions of the video frame in pixels.</summary>
        public int Height { get; private set; }
        /// <summary>Video only. The aspect ratio (width / height) which a single pixel should have when displayed.</summary>
        public Rational? Sample_aspect_ratio { get; private set; }
        /// <summary>Video only. Number of frames per second, for streams with constant frame durations. Should be set to { 0, 1 } when some frames have differing durations or if the value is not known.</summary>
        public Rational? Framerate { get; private set; }
        /// <summary>Video only. Number of delayed frames.</summary>
        public int Video_delay { get; private set; }
        internal VideoParam(AVCodecParameters* _codecpar)
        {
            PixelFormat = (AVPixelFormat)_codecpar->format;
            Bit_rate = _codecpar->bit_rate;
            Width = _codecpar->width;
            Height = _codecpar->height;
            var rat = _codecpar->sample_aspect_ratio;
            if (rat.den != 0) Sample_aspect_ratio = new Rational { Den = rat.den, Num = rat.num };
            var fr = _codecpar->framerate;
            Framerate = new Rational { Num = fr.num, Den = fr.den };
            Video_delay = _codecpar->video_delay;
        }

        public override string ToString()
        {
            return $"Video: {Width}x{Height}, BitRate:{Bit_rate} Format {PixelFormat}, Framerate {Framerate?.Num}:{Framerate?.Den}, Ratio {Sample_aspect_ratio?.Num}:{Sample_aspect_ratio?.Den}";
        }
    }
    public class Rational
    {
        /// <summary>Numerator</summary>
        public int Num;
        /// <summary>Denominator</summary>
        public int Den;
    }
    public unsafe class AudioParam
    {
        internal AVSampleFormat SampleFormat { get; set; }
        public long Bit_rate { get; private set; }
        public int ChanNm { get => Ch_layout.nb_channels; }
        /// <summary>Audio only. The channel layout and number of channels.</summary>
        internal AVChannelLayout Ch_layout { get; private set; }
        /// <summary>Audio only. The number of audio samples per second.</summary>
        public int Sample_rate { get; private set; }
        /// <summary>Audio only. The number of bytes per coded audio frame, required by some formats.</summary>
        public int Block_align { get; private set; }
        /// <summary>Audio only. Audio frame size, if known. Required by some formats to be static.</summary>
        public int Frame_size { get; private set; }
        /// <summary>Audio only. The amount of padding (in samples) inserted by the encoder at the beginning of the audio. I.e. this number of leading decoded samples must be discarded by the caller to get the original audio without leading padding.</summary>
        public int Initial_padding { get; private set; }
        /// <summary>Audio only. The amount of padding (in samples) appended by the encoder to the end of the audio. I.e. this number of decoded samples must be discarded by the caller from the end of the stream to get the original audio without any trailing padding.</summary>
        public int Trailing_padding { get; private set; }
        /// <summary>Audio only. Number of samples to skip after a discontinuity.</summary>
        public int Seek_preroll { get; private set; }
        internal AudioParam(AVCodecParameters* _codecpar)
        {
            SampleFormat = (AVSampleFormat)_codecpar->format;
            Bit_rate = _codecpar->bit_rate;
            Ch_layout = _codecpar->ch_layout;
            Sample_rate = _codecpar->sample_rate;
            Block_align = _codecpar->block_align;
            Frame_size = _codecpar->frame_size;
            Initial_padding = _codecpar->initial_padding;
            Trailing_padding = _codecpar->trailing_padding;
            Seek_preroll = _codecpar->seek_preroll;
        }
        public override string ToString()
        {
            return $"Audio: ChanNm {ChanNm}, BitRate:{Bit_rate}, SampleRate {Sample_rate}";
        }
    }

    public enum CodecID : int
    {
        UNKNOWN = -1,
        ID_NONE = 0,
        ID_MPEG1VIDEO = 1,
        /// <summary>preferred ID for MPEG-1/2 video decoding</summary>
        ID_MPEG2VIDEO = 2,
        ID_H261 = 3,
        ID_H263 = 4,
        ID_RV10 = 5,
        ID_RV20 = 6,
        ID_MJPEG = 7,
        ID_MJPEGB = 8,
        ID_LJPEG = 9,
        ID_SP5X = 10,
        ID_JPEGLS = 11,
        ID_MPEG4 = 12,
        ID_RAWVIDEO = 13,
        ID_MSMPEG4V1 = 14,
        ID_MSMPEG4V2 = 15,
        ID_MSMPEG4V3 = 16,
        ID_WMV1 = 17,
        ID_WMV2 = 18,
        ID_H263P = 19,
        ID_H263I = 20,
        ID_FLV1 = 21,
        ID_SVQ1 = 22,
        ID_SVQ3 = 23,
        ID_DVVIDEO = 24,
        ID_HUFFYUV = 25,
        ID_CYUV = 26,
        ID_H264 = 27,
        ID_INDEO3 = 28,
        ID_VP3 = 29,
        ID_THEORA = 30,
        ID_ASV1 = 31,
        ID_ASV2 = 32,
        ID_FFV1 = 33,
        ID_4XM = 34,
        ID_VCR1 = 35,
        ID_CLJR = 36,
        ID_MDEC = 37,
        ID_ROQ = 38,
        ID_INTERPLAY_VIDEO = 39,
        ID_XAN_WC3 = 40,
        ID_XAN_WC4 = 41,
        ID_RPZA = 42,
        ID_CINEPAK = 43,
        ID_WS_VQA = 44,
        ID_MSRLE = 45,
        ID_MSVIDEO1 = 46,
        ID_IDCIN = 47,
        ID_8BPS = 48,
        ID_SMC = 49,
        ID_FLIC = 50,
        ID_TRUEMOTION1 = 51,
        ID_VMDVIDEO = 52,
        ID_MSZH = 53,
        ID_ZLIB = 54,
        ID_QTRLE = 55,
        ID_TSCC = 56,
        ID_ULTI = 57,
        ID_QDRAW = 58,
        ID_VIXL = 59,
        ID_QPEG = 60,
        ID_PNG = 61,
        ID_PPM = 62,
        ID_PBM = 63,
        ID_PGM = 64,
        ID_PGMYUV = 65,
        ID_PAM = 66,
        ID_FFVHUFF = 67,
        ID_RV30 = 68,
        ID_RV40 = 69,
        ID_VC1 = 70,
        ID_WMV3 = 71,
        ID_LOCO = 72,
        ID_WNV1 = 73,
        ID_AASC = 74,
        ID_INDEO2 = 75,
        ID_FRAPS = 76,
        ID_TRUEMOTION2 = 77,
        ID_BMP = 78,
        ID_CSCD = 79,
        ID_MMVIDEO = 80,
        ID_ZMBV = 81,
        ID_AVS = 82,
        ID_SMACKVIDEO = 83,
        ID_NUV = 84,
        ID_KMVC = 85,
        ID_FLASHSV = 86,
        ID_CAVS = 87,
        ID_JPEG2000 = 88,
        ID_VMNC = 89,
        ID_VP5 = 90,
        ID_VP6 = 91,
        ID_VP6F = 92,
        ID_TARGA = 93,
        ID_DSICINVIDEO = 94,
        ID_TIERTEXSEQVIDEO = 95,
        ID_TIFF = 96,
        ID_GIF = 97,
        ID_DXA = 98,
        ID_DNXHD = 99,
        ID_THP = 100,
        ID_SGI = 101,
        ID_C93 = 102,
        ID_BETHSOFTVID = 103,
        ID_PTX = 104,
        ID_TXD = 105,
        ID_VP6A = 106,
        ID_AMV = 107,
        ID_VB = 108,
        ID_PCX = 109,
        ID_SUNRAST = 110,
        ID_INDEO4 = 111,
        ID_INDEO5 = 112,
        ID_MIMIC = 113,
        ID_RL2 = 114,
        ID_ESCAPE124 = 115,
        ID_DIRAC = 116,
        ID_BFI = 117,
        ID_CMV = 118,
        ID_MOTIONPIXELS = 119,
        ID_TGV = 120,
        ID_TGQ = 121,
        ID_TQI = 122,
        ID_AURA = 123,
        ID_AURA2 = 124,
        ID_V210X = 125,
        ID_TMV = 126,
        ID_V210 = 127,
        ID_DPX = 128,
        ID_MAD = 129,
        ID_FRWU = 130,
        ID_FLASHSV2 = 131,
        ID_CDGRAPHICS = 132,
        ID_R210 = 133,
        ID_ANM = 134,
        ID_BINKVIDEO = 135,
        ID_IFF_ILBM = 136,
        ID_KGV1 = 137,
        ID_YOP = 138,
        ID_VP8 = 139,
        ID_PICTOR = 140,
        ID_ANSI = 141,
        ID_A64_MULTI = 142,
        ID_A64_MULTI5 = 143,
        ID_R10K = 144,
        ID_MXPEG = 145,
        ID_LAGARITH = 146,
        ID_PRORES = 147,
        ID_JV = 148,
        ID_DFA = 149,
        ID_WMV3IMAGE = 150,
        ID_VC1IMAGE = 151,
        ID_UTVIDEO = 152,
        ID_BMV_VIDEO = 153,
        ID_VBLE = 154,
        ID_DXTORY = 155,
        ID_V410 = 156,
        ID_XWD = 157,
        ID_CDXL = 158,
        ID_XBM = 159,
        ID_ZEROCODEC = 160,
        ID_MSS1 = 161,
        ID_MSA1 = 162,
        ID_TSCC2 = 163,
        ID_MTS2 = 164,
        ID_CLLC = 165,
        ID_MSS2 = 166,
        ID_VP9 = 167,
        ID_AIC = 168,
        ID_ESCAPE130 = 169,
        ID_G2M = 170,
        ID_WEBP = 171,
        ID_HNM4_VIDEO = 172,
        ID_HEVC = 173,
        ID_FIC = 174,
        ID_ALIAS_PIX = 175,
        ID_BRENDER_PIX = 176,
        ID_PAF_VIDEO = 177,
        ID_EXR = 178,
        ID_VP7 = 179,
        ID_SANM = 180,
        ID_SGIRLE = 181,
        ID_MVC1 = 182,
        ID_MVC2 = 183,
        ID_HQX = 184,
        ID_TDSC = 185,
        ID_HQ_HQA = 186,
        ID_HAP = 187,
        ID_DDS = 188,
        ID_DXV = 189,
        ID_SCREENPRESSO = 190,
        ID_RSCC = 191,
        ID_AVS2 = 192,
        ID_PGX = 193,
        ID_AVS3 = 194,
        ID_MSP2 = 195,
        ID_VVC = 196,
        ID_Y41P = 197,
        ID_AVRP = 198,
        ID_012V = 199,
        ID_AVUI = 200,
        ID_TARGA_Y216 = 201,
        ID_V308 = 202,
        ID_V408 = 203,
        ID_YUV4 = 204,
        ID_AVRN = 205,
        ID_CPIA = 206,
        ID_XFACE = 207,
        ID_SNOW = 208,
        ID_SMVJPEG = 209,
        ID_APNG = 210,
        ID_DAALA = 211,
        ID_CFHD = 212,
        ID_TRUEMOTION2RT = 213,
        ID_M101 = 214,
        ID_MAGICYUV = 215,
        ID_SHEERVIDEO = 216,
        ID_YLC = 217,
        ID_PSD = 218,
        ID_PIXLET = 219,
        ID_SPEEDHQ = 220,
        ID_FMVC = 221,
        ID_SCPR = 222,
        ID_CLEARVIDEO = 223,
        ID_XPM = 224,
        ID_AV1 = 225,
        ID_BITPACKED = 226,
        ID_MSCC = 227,
        ID_SRGC = 228,
        ID_SVG = 229,
        ID_GDV = 230,
        ID_FITS = 231,
        ID_IMM4 = 232,
        ID_PROSUMER = 233,
        ID_MWSC = 234,
        ID_WCMV = 235,
        ID_RASC = 236,
        ID_HYMT = 237,
        ID_ARBC = 238,
        ID_AGM = 239,
        ID_LSCR = 240,
        ID_VP4 = 241,
        ID_IMM5 = 242,
        ID_MVDV = 243,
        ID_MVHA = 244,
        ID_CDTOONS = 245,
        ID_MV30 = 246,
        ID_NOTCHLC = 247,
        ID_PFM = 248,
        ID_MOBICLIP = 249,
        ID_PHOTOCD = 250,
        ID_IPU = 251,
        ID_ARGO = 252,
        ID_CRI = 253,
        ID_SIMBIOSIS_IMX = 254,
        ID_SGA_VIDEO = 255,
        ID_GEM = 256,
        ID_VBN = 257,
        ID_JPEGXL = 258,
        ID_QOI = 259,
        ID_PHM = 260,
        ID_RADIANCE_HDR = 261,
        ID_WBMP = 262,
        ID_MEDIA100 = 263,
        ID_VQC = 264,
        ID_PDV = 265,
        ID_EVC = 266,
        ID_RTV1 = 267,
        ID_VMIX = 268,
        ID_LEAD = 269,
        /// <summary>A dummy id pointing at the start of audio codecs</summary>
        ID_FIRST_AUDIO = 65536,
        ID_PCM_S16LE = 65536,
        ID_PCM_S16BE = 65537,
        ID_PCM_U16LE = 65538,
        ID_PCM_U16BE = 65539,
        ID_PCM_S8 = 65540,
        ID_PCM_U8 = 65541,
        ID_PCM_MULAW = 65542,
        ID_PCM_ALAW = 65543,
        ID_PCM_S32LE = 65544,
        ID_PCM_S32BE = 65545,
        ID_PCM_U32LE = 65546,
        ID_PCM_U32BE = 65547,
        ID_PCM_S24LE = 65548,
        ID_PCM_S24BE = 65549,
        ID_PCM_U24LE = 65550,
        ID_PCM_U24BE = 65551,
        ID_PCM_S24DAUD = 65552,
        ID_PCM_ZORK = 65553,
        ID_PCM_S16LE_PLANAR = 65554,
        ID_PCM_DVD = 65555,
        ID_PCM_F32BE = 65556,
        ID_PCM_F32LE = 65557,
        ID_PCM_F64BE = 65558,
        ID_PCM_F64LE = 65559,
        ID_PCM_BLURAY = 65560,
        ID_PCM_LXF = 65561,
        ID_S302M = 65562,
        ID_PCM_S8_PLANAR = 65563,
        ID_PCM_S24LE_PLANAR = 65564,
        ID_PCM_S32LE_PLANAR = 65565,
        ID_PCM_S16BE_PLANAR = 65566,
        ID_PCM_S64LE = 65567,
        ID_PCM_S64BE = 65568,
        ID_PCM_F16LE = 65569,
        ID_PCM_F24LE = 65570,
        ID_PCM_VIDC = 65571,
        ID_PCM_SGA = 65572,
        ID_ADPCM_IMA_QT = 69632,
        ID_ADPCM_IMA_WAV = 69633,
        ID_ADPCM_IMA_DK3 = 69634,
        ID_ADPCM_IMA_DK4 = 69635,
        ID_ADPCM_IMA_WS = 69636,
        ID_ADPCM_IMA_SMJPEG = 69637,
        ID_ADPCM_MS = 69638,
        ID_ADPCM_4XM = 69639,
        ID_ADPCM_XA = 69640,
        ID_ADPCM_ADX = 69641,
        ID_ADPCM_EA = 69642,
        ID_ADPCM_G726 = 69643,
        ID_ADPCM_CT = 69644,
        ID_ADPCM_SWF = 69645,
        ID_ADPCM_YAMAHA = 69646,
        ID_ADPCM_SBPRO_4 = 69647,
        ID_ADPCM_SBPRO_3 = 69648,
        ID_ADPCM_SBPRO_2 = 69649,
        ID_ADPCM_THP = 69650,
        ID_ADPCM_IMA_AMV = 69651,
        ID_ADPCM_EA_R1 = 69652,
        ID_ADPCM_EA_R3 = 69653,
        ID_ADPCM_EA_R2 = 69654,
        ID_ADPCM_IMA_EA_SEAD = 69655,
        ID_ADPCM_IMA_EA_EACS = 69656,
        ID_ADPCM_EA_XAS = 69657,
        ID_ADPCM_EA_MAXIS_XA = 69658,
        ID_ADPCM_IMA_ISS = 69659,
        ID_ADPCM_G722 = 69660,
        ID_ADPCM_IMA_APC = 69661,
        ID_ADPCM_VIMA = 69662,
        ID_ADPCM_AFC = 69663,
        ID_ADPCM_IMA_OKI = 69664,
        ID_ADPCM_DTK = 69665,
        ID_ADPCM_IMA_RAD = 69666,
        ID_ADPCM_G726LE = 69667,
        ID_ADPCM_THP_LE = 69668,
        ID_ADPCM_PSX = 69669,
        ID_ADPCM_AICA = 69670,
        ID_ADPCM_IMA_DAT4 = 69671,
        ID_ADPCM_MTAF = 69672,
        ID_ADPCM_AGM = 69673,
        ID_ADPCM_ARGO = 69674,
        ID_ADPCM_IMA_SSI = 69675,
        ID_ADPCM_ZORK = 69676,
        ID_ADPCM_IMA_APM = 69677,
        ID_ADPCM_IMA_ALP = 69678,
        ID_ADPCM_IMA_MTF = 69679,
        ID_ADPCM_IMA_CUNNING = 69680,
        ID_ADPCM_IMA_MOFLEX = 69681,
        ID_ADPCM_IMA_ACORN = 69682,
        ID_ADPCM_XMD = 69683,
        ID_AMR_NB = 73728,
        ID_AMR_WB = 73729,
        ID_RA_144 = 77824,
        ID_RA_288 = 77825,
        ID_ROQ_DPCM = 81920,
        ID_INTERPLAY_DPCM = 81921,
        ID_XAN_DPCM = 81922,
        ID_SOL_DPCM = 81923,
        ID_SDX2_DPCM = 81924,
        ID_GREMLIN_DPCM = 81925,
        ID_DERF_DPCM = 81926,
        ID_WADY_DPCM = 81927,
        ID_CBD2_DPCM = 81928,
        ID_MP2 = 86016,
        /// <summary>preferred ID for decoding MPEG audio layer 1, 2 or 3</summary>
        ID_MP3 = 86017,
        ID_AAC = 86018,
        ID_AC3 = 86019,
        ID_DTS = 86020,
        ID_VORBIS = 86021,
        ID_DVAUDIO = 86022,
        ID_WMAV1 = 86023,
        ID_WMAV2 = 86024,
        ID_MACE3 = 86025,
        ID_MACE6 = 86026,
        ID_VMDAUDIO = 86027,
        ID_FLAC = 86028,
        ID_MP3ADU = 86029,
        ID_MP3ON4 = 86030,
        ID_SHORTEN = 86031,
        ID_ALAC = 86032,
        ID_WESTWOOD_SND1 = 86033,
        /// <summary>as in Berlin toast format</summary>
        ID_GSM = 86034,
        ID_QDM2 = 86035,
        ID_COOK = 86036,
        ID_TRUESPEECH = 86037,
        ID_TTA = 86038,
        ID_SMACKAUDIO = 86039,
        ID_QCELP = 86040,
        ID_WAVPACK = 86041,
        ID_DSICINAUDIO = 86042,
        ID_IMC = 86043,
        ID_MUSEPACK7 = 86044,
        ID_MLP = 86045,
        ID_GSM_MS = 86046,
        ID_ATRAC3 = 86047,
        ID_APE = 86048,
        ID_NELLYMOSER = 86049,
        ID_MUSEPACK8 = 86050,
        ID_SPEEX = 86051,
        ID_WMAVOICE = 86052,
        ID_WMAPRO = 86053,
        ID_WMALOSSLESS = 86054,
        ID_ATRAC3P = 86055,
        ID_EAC3 = 86056,
        ID_SIPR = 86057,
        ID_MP1 = 86058,
        ID_TWINVQ = 86059,
        ID_TRUEHD = 86060,
        ID_MP4ALS = 86061,
        ID_ATRAC1 = 86062,
        ID_BINKAUDIO_RDFT = 86063,
        ID_BINKAUDIO_DCT = 86064,
        ID_AAC_LATM = 86065,
        ID_QDMC = 86066,
        ID_CELT = 86067,
        ID_G723_1 = 86068,
        ID_G729 = 86069,
        ID_8SVX_EXP = 86070,
        ID_8SVX_FIB = 86071,
        ID_BMV_AUDIO = 86072,
        ID_RALF = 86073,
        ID_IAC = 86074,
        ID_ILBC = 86075,
        ID_OPUS = 86076,
        ID_COMFORT_NOISE = 86077,
        ID_TAK = 86078,
        ID_METASOUND = 86079,
        ID_PAF_AUDIO = 86080,
        ID_ON2AVC = 86081,
        ID_DSS_SP = 86082,
        ID_CODEC2 = 86083,
        ID_FFWAVESYNTH = 86084,
        ID_SONIC = 86085,
        ID_SONIC_LS = 86086,
        ID_EVRC = 86087,
        ID_SMV = 86088,
        ID_DSD_LSBF = 86089,
        ID_DSD_MSBF = 86090,
        ID_DSD_LSBF_PLANAR = 86091,
        ID_DSD_MSBF_PLANAR = 86092,
        ID_4GV = 86093,
        ID_INTERPLAY_ACM = 86094,
        ID_XMA1 = 86095,
        ID_XMA2 = 86096,
        ID_DST = 86097,
        ID_ATRAC3AL = 86098,
        ID_ATRAC3PAL = 86099,
        ID_DOLBY_E = 86100,
        ID_APTX = 86101,
        ID_APTX_HD = 86102,
        ID_SBC = 86103,
        ID_ATRAC9 = 86104,
        ID_HCOM = 86105,
        ID_ACELP_KELVIN = 86106,
        ID_MPEGH_3D_AUDIO = 86107,
        ID_SIREN = 86108,
        ID_HCA = 86109,
        ID_FASTAUDIO = 86110,
        ID_MSNSIREN = 86111,
        ID_DFPWM = 86112,
        ID_BONK = 86113,
        ID_MISC4 = 86114,
        ID_APAC = 86115,
        ID_FTR = 86116,
        ID_WAVARC = 86117,
        ID_RKA = 86118,
        ID_AC4 = 86119,
        ID_OSQ = 86120,
        ID_QOA = 86121,
        ID_LC3 = 86122,
        /// <summary>A dummy ID pointing at the start of subtitle codecs.</summary>
        ID_FIRST_SUBTITLE = 94208,
        ID_DVD_SUBTITLE = 94208,
        ID_DVB_SUBTITLE = 94209,
        /// <summary>raw UTF-8 text</summary>
        ID_TEXT = 94210,
        ID_XSUB = 94211,
        ID_SSA = 94212,
        ID_MOV_TEXT = 94213,
        ID_HDMV_PGS_SUBTITLE = 94214,
        ID_DVB_TELETEXT = 94215,
        ID_SRT = 94216,
        ID_MICRODVD = 94217,
        ID_EIA_608 = 94218,
        ID_JACOSUB = 94219,
        ID_SAMI = 94220,
        ID_REALTEXT = 94221,
        ID_STL = 94222,
        ID_SUBVIEWER1 = 94223,
        ID_SUBVIEWER = 94224,
        ID_SUBRIP = 94225,
        ID_WEBVTT = 94226,
        ID_MPL2 = 94227,
        ID_VPLAYER = 94228,
        ID_PJS = 94229,
        ID_ASS = 94230,
        ID_HDMV_TEXT_SUBTITLE = 94231,
        ID_TTML = 94232,
        ID_ARIB_CAPTION = 94233,
        /// <summary>A dummy ID pointing at the start of various fake codecs.</summary>
        ID_FIRST_UNKNOWN = 98304,
        ID_TTF = 98304,
        /// <summary>Contain timestamp estimated through PCR of program stream.</summary>
        ID_SCTE_35 = 98305,
        ID_EPG = 98306,
        ID_BINTEXT = 98307,
        ID_XBIN = 98308,
        ID_IDF = 98309,
        ID_OTF = 98310,
        ID_SMPTE_KLV = 98311,
        ID_DVD_NAV = 98312,
        ID_TIMED_ID3 = 98313,
        ID_BIN_DATA = 98314,
        ID_SMPTE_2038 = 98315,
        ID_LCEVC = 98316,
        /// <summary>codec_id is not known (like AV_CODEC_ID_NONE) but lavf should attempt to identify it</summary>
        ID_PROBE = 102400,
        /// <summary>_FAKE_ codec to indicate a raw MPEG-2 TS stream (only used by libavformat)</summary>
        ID_MPEG2TS = 131072,
        /// <summary>_FAKE_ codec to indicate a MPEG-4 Systems stream (only used by libavformat)</summary>
        ID_MPEG4SYSTEMS = 131073,
        /// <summary>Dummy codec for streams containing only metadata information.</summary>
        ID_FFMETADATA = 135168,
        /// <summary>Passthrough codec, AVFrames wrapped in AVPacket</summary>
        ID_WRAPPED_AVFRAME = 135169,
        /// <summary>Dummy null video codec, useful mainly for development and debugging. Null encoder/decoder discard all input and never return any output.</summary>
        ID_VNULL = 135170,
        /// <summary>Dummy null audio codec, useful mainly for development and debugging. Null encoder/decoder discard all input and never return any output.</summary>
        ID_ANULL = 135171,
    }
}
