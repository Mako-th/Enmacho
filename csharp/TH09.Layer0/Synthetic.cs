using System.Text;

namespace TH09.Layer0;

public static class Synthetic
{
    public sealed record Case(string Label, Dictionary<string, uint[]> Columns,
                              List<string> Fields, int RecordVersion,
                              bool Compress, bool OmitFields,
                              TickEncoder.SectionPolicy? Policy = null,
                              Func<JsonNodeLite, JsonNodeLite>? MangleFieldOrder = null);

    public static TickEncoder.SectionPolicy StairPolicy(params string[] names) => new()
    {
        InvariantHint = new HashSet<string>(StringComparer.Ordinal),
        ChangeHint = new HashSet<string>(names, StringComparer.Ordinal),
    };

    public static List<Case> All()
    {
        var cases = new List<Case>();

        cases.Add(Simple("空（tick_count=0 / 列 0 本）", []));
        cases.Add(Simple("列 1 本 × 1 tick（_pack_best が差分を試さない）",
            [("only_one", [0x12345678u])]));
        cases.Add(Simple("列 1 本 × 2 tick（差分を試す最小）",
            [("two_ticks", [0u, 0xFFFFFFFFu])]));

        cases.Add(Simple("全語 0xFFFFFFFF の不変列 ＋ 全語 0 の不変列",
            [("all_ff", Fill(0xFFFFFFFFu, 64)), ("all_zero", Fill(0u, 64))]));
        cases.Add(Simple("単調減少（差分が全語 0xFFFFFFFF になる）",
            [("countdown", Range(200, i => (uint)(200 - i)))]));
        cases.Add(Simple("0 をまたいで巻き戻る（u32 の巡回）",
            [("wrap", Range(200, i => unchecked((uint)(3 - i))))]));
        cases.Add(Simple("最上位ビットが立つ値だけ（i32 として負）",
            [("negative_bits", Range(200, i => 0x80000000u | (uint)i))]));

        cases.Add(new Case("毎 tick 変化する changes 列（count == tick_count）",
            Map([("stair_every_tick", Range(300, i => (uint)i * 7u)),
                 ("stair_constant", Fill(5u, 300))]),
            ["stair_every_tick", "stair_constant"], 13, true, false,
            Policy: StairPolicy("stair_every_tick", "stair_constant")));

        cases.Add(new Case("changes 列の値が減る（values_transform=delta が巻き戻る）",
            Map([("stair_down", Range(300, i => unchecked((uint)(10 - i / 3))))]),
            ["stair_down"], 13, true, false,
            Policy: StairPolicy("stair_down")));

        cases.Add(new Case("changes 列が最後の tick でだけ動く（末尾の敷き詰め）",
            Map([("stair_last", Range(300, i => i == 299 ? 1u : 0u))]),
            ["stair_last"], 13, true, false,
            Policy: StairPolicy("stair_last")));

        cases.Add(Simple("列名が非 ASCII・引用符・制御文字（JSON の逃がし方）",
            [("名前付き", Range(32, i => (uint)i)),
             ("quote\"back\\slash", Range(32, i => (uint)(i * 3))),
             ("ctrl\u001funit", Fill(1u, 32)),
             ("tab\there", Fill(2u, 32))]));

        var slots = SlotLike(24, 6);
        cases.Add(new Case("field_order を zlib で包まない（json）", slots.Columns, slots.Fields, 13,
                           Compress: false, OmitFields: false));
        cases.Add(new Case("field_order を包まず列名も落とす（json-nofields）", slots.Columns, slots.Fields, 13,
                           Compress: false, OmitFields: true));
        cases.Add(new Case("zlib で包むが列名は落とさない（json-zlib）", slots.Columns, slots.Fields, 13,
                           Compress: true, OmitFields: false));

        cases.Add(new Case("未知の区画を 1 チャンクぶん挟む（前方互換の読み飛ばし）",
            slots.Columns, slots.Fields, 13, true, false,
            MangleFieldOrder: fo => InsertUnknownSection(fo, "future-v2", 1)));

        cases.Add(new Case("未知の区画を 2 チャンクぶん挟む",
            slots.Columns, slots.Fields, 13, true, false,
            MangleFieldOrder: fo => InsertUnknownSection(fo, "future-v3", 2)));

        return cases;
    }

    private static (Dictionary<string, uint[]> Columns, List<string> Fields) SlotLike(int ticks, int slots)
    {
        var cols = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        var fields = new List<string>();
        for (int s = 0; s < slots; s++)
        {
            Add($"p1_b{s}_x", Range(ticks, i => unchecked((uint)BitConverter.SingleToInt32Bits(10.5f + i * 0.25f + s))));
            Add($"p1_b{s}_y", Range(ticks, i => unchecked((uint)BitConverter.SingleToInt32Bits(-3.75f * (i % 5) - s))));
            Add($"p1_b{s}_kind", Range(ticks, i => (uint)((i + s) % 4)));
            Add($"p1_b{s}_state", Fill((uint)(s % 2), ticks));
        }
        return (cols, fields);

        void Add(string name, uint[] col) { cols[name] = col; fields.Add(name); }
    }

    private static Dictionary<string, uint[]> Map((string Name, uint[] Col)[] cols)
    {
        var map = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        foreach (var (n, c) in cols) map[n] = c;
        return map;
    }

    private static Case Simple(string label, (string Name, uint[] Col)[] cols) =>
        new(label, Map(cols), cols.Select(x => x.Name).ToList(), 13, true, false);

    private static uint[] Fill(uint value, int n)
    {
        var a = new uint[n];
        a.AsSpan().Fill(value);
        return a;
    }

    private static uint[] Range(int n, Func<int, uint> f)
    {
        var a = new uint[n];
        for (int i = 0; i < n; i++) a[i] = f(i);
        return a;
    }

    private static JsonNodeLite InsertUnknownSection(JsonNodeLite fieldOrder, string kind, int chunks)
    {
        var sections = fieldOrder.Array("sections").ToList();
        var unknown = JsonNodeLite.FromObject(
        [
            new("kind", JsonNodeLite.FromString(kind)),
            new("chunks", JsonNodeLite.FromInt(chunks)),
            new("note", JsonNodeLite.FromString("将来の区画。読む側は chunks 個だけ読み飛ばす")),
        ]);
        sections.Insert(1, unknown);
        return fieldOrder.WithMember("sections", JsonNodeLite.FromArray(sections));
    }

    public static RoundTripPack.Record BuildRecord(Case c, long no)
    {
        var enc = TickEncoder.EncodeColumns(c.Columns, c.Fields,
                                            c.Policy ?? TickEncoder.SectionPolicy.Empty, c.RecordVersion);
        var fieldOrder = enc.FieldOrder;
        var blob = enc.Blob;

        if (c.MangleFieldOrder is not null)
        {
            var before = fieldOrder.Array("sections").Count;
            fieldOrder = c.MangleFieldOrder(fieldOrder);
            if (fieldOrder.Array("sections").Count != before + 1)
                throw new InvalidOperationException("未知の区画の差し込みが 1 個になっていません");
            int chunks = (int)fieldOrder.Array("sections")[1].Int("chunks", 0);
            var filler = new List<byte>();
            for (int i = 0; i < chunks; i++)
                filler.AddRange(TickArchive.PackChunk([0xDEADBEEFu, 0xFEEDFACEu], TickArchive.DefaultLevel));
            int head = FirstChunkLength(blob);
            var merged = new byte[blob.Length + filler.Count];
            Buffer.BlockCopy(blob, 0, merged, 0, head);
            filler.CopyTo(merged, head);
            Buffer.BlockCopy(blob, head, merged, head + filler.Count, blob.Length - head);
            blob = merged;
        }

        var sections = TickArchive.ParseFieldOrder(fieldOrder.ToCompactJson(escapeNonAscii: false)).Sections;
        var plain = c.OmitFields
            ? fieldOrder.WithMember("fields", JsonNodeLite.FromStrings(TickArchive.FieldsFromSections(sections)))
            : fieldOrder;

        var (packed, packedEncoding) = TickArchive.PackFieldOrder(fieldOrder, sections, c.Compress, c.OmitFields);

        return new RoundTripPack.Record
        {
            Kind = "S",
            SessionId = -1,
            No = no,
            Label = c.Label,
            FieldOrderUtf8 = Encoding.UTF8.GetBytes(plain.ToCompactJson(escapeNonAscii: false)),
            FieldOrderEncoding = packedEncoding,
            FieldOrderPacked = packed,
            Blob = blob,
            Expected = c.Columns,
        };
    }

    private static int FirstChunkLength(byte[] blob)
    {
        uint compLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(4));
        return 8 + (int)compLen;
    }
}
