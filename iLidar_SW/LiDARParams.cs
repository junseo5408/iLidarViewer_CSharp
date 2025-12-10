using System;
using System.Collections.Generic;
using System.Text;

public class LiDARParams
{
    // 딕셔너리 대신 속성을 사용하여 C# 스타일로 변환
    public ushort SensorSn { get; set; }
    public byte CaptureMode { get; set; }
    public byte CaptureRow { get; set; }
    public ushort[] CaptureShutter { get; set; } = new ushort[5];
    public ushort[] CaptureLimit { get; set; } = new ushort[2];
    public uint CapturePeriodUs { get; set; }
    public byte CaptureSeq { get; set; }
    public byte DataOutput { get; set; }
    public uint DataBaud { get; set; }
    public byte[] DataSensorIp { get; set; } = new byte[4];
    public byte[] DataDestIp { get; set; } = new byte[4];
    public byte[] DataSubnet { get; set; } = new byte[4];
    public byte[] DataGateway { get; set; } = new byte[4];
    public ushort DataPort { get; set; }
    public byte[] DataMacAddr { get; set; } = new byte[6];
    public byte Sync { get; set; }
    public uint SyncTrigDelayUs { get; set; }
    public ushort[] SyncIllDelayUs { get; set; } = new ushort[15];
    public byte SyncTrigTrimUs { get; set; }
    public byte SyncIllTrimUs { get; set; }
    public ushort SyncOutputDelayUs { get; set; }
    public byte Arb { get; set; }
    public uint ArbTimeout { get; set; }

    // Decode: byte[] -> Class Object
    public static LiDARParams Decode(byte[] src)
    {
        var p = new LiDARParams();
        p.SensorSn = (ushort)((src[1] << 8) | src[0]);

        // ... (필요 시 HW ID, FW 버전 등 추가 파싱 가능) ...

        p.CaptureMode = src[71];
        p.CaptureRow = src[72];

        for (int i = 0; i < 5; i++)
            p.CaptureShutter[i] = (ushort)((src[74 + i * 2] << 8) | src[73 + i * 2]);

        for (int i = 0; i < 2; i++)
            p.CaptureLimit[i] = (ushort)((src[84 + i * 2] << 8) | src[83 + i * 2]);

        p.CapturePeriodUs = (uint)((src[90] << 24) | (src[89] << 16) | (src[88] << 8) | src[87]);
        p.CaptureSeq = src[91];
        p.DataOutput = src[92];
        p.DataBaud = (uint)((src[96] << 24) | (src[95] << 16) | (src[94] << 8) | src[93]);

        Array.Copy(src, 97, p.DataSensorIp, 0, 4);
        Array.Copy(src, 101, p.DataDestIp, 0, 4);
        Array.Copy(src, 105, p.DataSubnet, 0, 4);
        Array.Copy(src, 109, p.DataGateway, 0, 4);

        p.DataPort = (ushort)((src[114] << 8) | src[113]);
        Array.Copy(src, 115, p.DataMacAddr, 0, 6);

        p.Sync = src[121];
        p.SyncTrigDelayUs = (uint)((src[125] << 24) | (src[124] << 16) | (src[123] << 8) | src[122]);

        for (int i = 0; i < 15; i++)
            p.SyncIllDelayUs[i] = (ushort)((src[127 + i * 2] << 8) | src[126 + i * 2]);

        p.SyncTrigTrimUs = src[156];
        p.SyncIllTrimUs = src[157];
        p.SyncOutputDelayUs = (ushort)((src[159] << 8) | src[158]);

        p.Arb = src[160];
        p.ArbTimeout = (uint)((src[164] << 24) | (src[163] << 16) | (src[162] << 8) | src[161]);

        return p;
    }

    // Encode: Class Object -> byte[]
    public byte[] Encode()
    {
        byte[] dst = new byte[166];

        dst[0] = (byte)(SensorSn % 256);
        dst[1] = (byte)(SensorSn / 256);

        // 2~70은 0으로 채움 (C# 배열 초기화시 자동 0이지만 명시적으로 루프 가능)

        dst[71] = CaptureMode;
        dst[72] = CaptureRow;

        for (int i = 0; i < 5; i++)
        {
            dst[73 + i * 2] = (byte)((CaptureShutter[i] >> 0) & 0xFF);
            dst[74 + i * 2] = (byte)((CaptureShutter[i] >> 8) & 0xFF);
        }

        dst[83] = (byte)((CaptureLimit[0] >> 0) & 0xFF);
        dst[84] = (byte)((CaptureLimit[0] >> 8) & 0xFF);
        dst[85] = (byte)((CaptureLimit[1] >> 0) & 0xFF);
        dst[86] = (byte)((CaptureLimit[1] >> 8) & 0xFF);

        dst[87] = (byte)((CapturePeriodUs >> 0) & 0xFF);
        dst[88] = (byte)((CapturePeriodUs >> 8) & 0xFF);
        dst[89] = (byte)((CapturePeriodUs >> 16) & 0xFF);
        dst[90] = (byte)((CapturePeriodUs >> 24) & 0xFF);

        dst[91] = CaptureSeq;
        dst[92] = DataOutput;

        dst[93] = (byte)((DataBaud >> 0) & 0xFF);
        dst[94] = (byte)((DataBaud >> 8) & 0xFF);
        dst[95] = (byte)((DataBaud >> 16) & 0xFF);
        dst[96] = (byte)((DataBaud >> 24) & 0xFF);

        Array.Copy(DataSensorIp, 0, dst, 97, 4);
        Array.Copy(DataDestIp, 0, dst, 101, 4);
        Array.Copy(DataSubnet, 0, dst, 105, 4);
        Array.Copy(DataGateway, 0, dst, 109, 4);

        dst[113] = (byte)((DataPort >> 0) & 0xFF);
        dst[114] = (byte)((DataPort >> 8) & 0xFF);

        Array.Copy(DataMacAddr, 0, dst, 115, 6);

        dst[121] = Sync;
        dst[122] = (byte)((SyncTrigDelayUs >> 0) & 0xFF);
        dst[123] = (byte)((SyncTrigDelayUs >> 8) & 0xFF);
        dst[124] = (byte)((SyncTrigDelayUs >> 16) & 0xFF);
        dst[125] = (byte)((SyncTrigDelayUs >> 24) & 0xFF);

        for (int i = 0; i < 15; i++)
        {
            dst[126 + i * 2] = (byte)((SyncIllDelayUs[i] >> 0) & 0xFF);
            dst[127 + i * 2] = (byte)((SyncIllDelayUs[i] >> 8) & 0xFF);
        }

        dst[156] = SyncTrigTrimUs;
        dst[157] = SyncIllTrimUs;

        dst[158] = (byte)((SyncOutputDelayUs >> 0) & 0xFF);
        dst[159] = (byte)((SyncOutputDelayUs >> 8) & 0xFF);

        dst[160] = Arb;
        dst[161] = (byte)((ArbTimeout >> 0) & 0xFF);
        dst[162] = (byte)((ArbTimeout >> 8) & 0xFF);
        dst[163] = (byte)((ArbTimeout >> 16) & 0xFF);
        dst[164] = (byte)((ArbTimeout >> 24) & 0xFF);

        return dst;
    }

    public LiDARParams Clone()
    {
        // 간단한 Deep Copy 구현 (인코딩 후 다시 디코딩)
        return Decode(Encode());
    }
}