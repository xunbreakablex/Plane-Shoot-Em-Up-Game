using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Hangar: ucak modelini sec, parca slotlarini doldur, savasa basla.
// Yerlesim (sanal 1280x720): ust seritte ucak sekmeleri ve boya; solda kategori sekmeli slot kartlari;
// sagda performans / savas / guc ozeti; ortada buyuk ucak onizlemesi; altta "SAVASA BASLA".
// Secimler her model icin ayri ve kalici (PlayerPrefs) saklanir.
public class HangarController : MonoBehaviour
{
    public GameObject[] modelPrefabs;
    public Transform previewRoot;
    public Camera cam;

    private enum Tab { Body, Weapons, Missiles, Shields, Turrets }

    private struct Slot
    {
        public string title;
        public SlotKind kind;
        public int index;
        public PartDef[] parts;
        public int current;
        public bool locked;
        public string requirement;
    }

    private static readonly string[] TabNames = { "GÖVDE", "SİLAH", "FÜZE", "KALKAN", "TARET" };
    private static readonly Color[] TabColors =
    {
        new Color(0.55f, 0.75f, 1f), new Color(1f, 0.6f, 0.25f), new Color(1f, 0.4f, 0.4f),
        new Color(0.4f, 0.85f, 1f), new Color(0.75f, 0.6f, 1f)
    };

    private Tab tab = Tab.Body;
    private int model;
    private PlaneLoadout loadout;
    private GameObject preview;

    private float yaw = 25f;
    private float zoom = 1f;
    private const float IdleSpin = 12f;
    private float lastInteraction;

    private string toast;
    private float toastUntil;
    private string lastDesc = "Bir parçanın üzerine gel: ayrıntısı burada görünür.";
    private bool showHelp;

    private LoadoutStats cur, prev;
    private bool hasPrev;

    private const float TopH = 64f;
    private const float BottomH = 96f;
    private const float LeftW = 392f;
    private const float RightW = 340f;
    private const float Gap = 14f;

    private const string HelpSeenKey = "PlaneShootEmUp.HangarHelpSeen";

    private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    private static readonly Color Dim = new Color(0.62f, 0.68f, 0.76f);

    private void Start()
    {
        GameState.Paused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        model = Mathf.Clamp(LoadoutStore.SelectedModel, 0, AircraftCatalog.All.Length - 1);
        if (!Unlocks.OwnsAircraft(model)) model = 0;
        if (cam == null) cam = Camera.main;
        RebuildPreview();
        Unlocks.Changed += OnUnlocksChanged;

        // Ilk ziyarette "nasil oynanir" kendiliginden acilir
        if (PlayerPrefs.GetInt(HelpSeenKey, 0) == 0)
        {
            showHelp = true;
            PlayerPrefs.SetInt(HelpSeenKey, 1);
        }
    }

    private void OnDestroy()
    {
        Unlocks.Changed -= OnUnlocksChanged;
    }

    // Konsoldan (unlockall / lockall) ya da yeni boss yenilgisiyle kilitler degisti
    private void OnUnlocksChanged()
    {
        LoadoutStore.Resanitize();
        RebuildPreview();
    }

    // ------------------------------------------------------------- preview

    private void RebuildPreview()
    {
        loadout = LoadoutStore.Get(model);
        cur = LoadoutMath.Compute(loadout);
        prev = cur;
        hasPrev = false;

        if (preview != null) Destroy(preview);
        if (modelPrefabs == null || model >= modelPrefabs.Length || modelPrefabs[model] == null) return;

        preview = Instantiate(modelPrefabs[model], previewRoot);
        preview.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        preview.transform.localRotation = Quaternion.identity;

        // Onizleme: fizik ve iz efektleri gereksiz
        foreach (var c in preview.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var t in preview.GetComponentsInChildren<TrailRenderer>()) Destroy(t.gameObject);
        foreach (var p in preview.GetComponentsInChildren<Propeller>()) p.Rpm = 900f;

        PlaneBuilder.Apply(preview, model, loadout, false);

        // Kalkan balonu ucagi bogmasin: kalkan degerleri sagdaki ozette gorunur
        foreach (var t in preview.GetComponentsInChildren<Transform>())
        {
            if (t != null && t.name == PlaneBuilder.Prefix + "Shield")
                Destroy(t.gameObject);
        }
    }

    private void Update()
    {
        Rect free = FreeRegion();

        if (previewRoot != null)
        {
            var mouse = Mouse.current;
            if (mouse != null && !showHelp)
            {
                Vector2 p = Hud.ToHud(mouse.position.ReadValue());
                bool overViewport = free.Contains(p);

                if (overViewport && mouse.leftButton.isPressed)
                {
                    yaw -= mouse.delta.ReadValue().x * 0.35f;
                    lastInteraction = Time.time;
                }
                if (overViewport)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        zoom = Mathf.Clamp(zoom - Mathf.Sign(scroll) * 0.08f, 0.55f, 1.4f);
                        lastInteraction = Time.time;
                    }
                }
            }

            if (Time.time - lastInteraction > 2f) yaw += IdleSpin * Time.deltaTime;
            previewRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        if (cam != null)
        {
            // Ucak, sol/sag panellerin arasindaki bos alanin ortasinda dursun
            float W = Hud.W, H = Hud.H;
            float offX = (free.center.x - W * 0.5f) / W;
            float offY = (free.center.y - H * 0.5f) / H;

            Vector3 center = new Vector3(0f, 1.2f, 0f);
            Vector3 dir = new Vector3(0f, 0.22f, -1f).normalized;
            float dist = 21f * zoom;

            float visH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visW = visH * cam.aspect;

            cam.transform.position = center + dir * dist;
            cam.transform.LookAt(center - Vector3.right * (offX * visW) + Vector3.up * (offY * visH));
        }
    }

    // Panellerin arasinda kalan bos alan (HUD birimi)
    private Rect FreeRegion()
    {
        float W = Hud.W, H = Hud.H;
        float x0 = Gap + LeftW + Gap;
        float x1 = W - Gap - RightW - Gap;
        float y0 = TopH + 8f;
        float y1 = H - BottomH - 4f;
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    // ---------------------------------------------------------------- slots

    private void Commit()
    {
        LoadoutStore.Save();

        prev = cur;
        hasPrev = true;
        RebuildPreviewKeepDelta();
    }

    private void RebuildPreviewKeepDelta()
    {
        LoadoutStats before = prev;
        RebuildPreview();
        prev = before;
        hasPrev = true;
    }

    private string CycleModel(int model2)
    {
        model = model2;
        if (Unlocks.OwnsAircraft(model)) LoadoutStore.SelectedModel = model;
        RebuildPreview();

        AircraftDef a = AircraftCatalog.Get(model);
        string head = $"{a.name}: {a.tagline}";
        if (!Unlocks.OwnsAircraft(model))
            return head + "  KİLİTLİ — " + Unlocks.AircraftRequirement(model) + ".";
        return head;
    }

    // Sekmeye sigsin diye kisaltilmis ad
    private static string ShortName(string name)
    {
        switch (name)
        {
            case "Çift Kanatlı (WW1)": return "WW1 Çift Kanat";
            case "Avcı (WW2)": return "WW2 Avcı";
            case "Yavru Köpekbalığı": return "Yavru Köpekb.";
            default: return name;
        }
    }

    private string CyclePaint(int dir)
    {
        int count = PartCatalog.Paints.Length;
        loadout.paint = ((loadout.paint + dir) % count + count) % count;
        LoadoutStore.Save();
        RebuildPreviewKeepDelta();
        return "Gövde, kanat ve vurgu renklerini değiştirir. Performansı etkilemez.";
    }

    private string CycleSlot(Slot s, int dir)
    {
        if (s.locked)
        {
            Toast("Bu slot kilitli: " + s.requirement);
            return s.requirement;
        }

        int count = s.parts.Length;
        int next = s.current;
        bool found = false;

        // Guc butcesine sigmayan secenekleri atla
        for (int step = 1; step <= count; step++)
        {
            int candidate = ((s.current + dir * step) % count + count) % count;
            if (candidate == s.current) break;
            if (LoadoutMath.FitsPower(loadout, s.kind, s.index, candidate))
            {
                next = candidate;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Toast("Güç yetersiz! Daha güçlü bir motor tak ya da başka bir parçayı çıkar.");
            return s.parts[s.current].description;
        }

        LoadoutMath.Set(loadout, s.kind, s.index, next);
        Commit();
        return s.parts[next].description;
    }

    private void Toast(string message)
    {
        toast = message;
        toastUntil = Time.unscaledTime + 2.8f;
    }

    private List<Slot> BuildSlots()
    {
        var list = new List<Slot>();
        switch (tab)
        {
            case Tab.Body:
                list.Add(new Slot { title = "Motor / Pervane", kind = SlotKind.Engine, index = 0, parts = PartCatalog.Engines, current = loadout.engine });
                list.Add(new Slot { title = "Kanatlar", kind = SlotKind.Wings, index = 0, parts = PartCatalog.Wings, current = loadout.wings });
                list.Add(new Slot { title = "Zırh", kind = SlotKind.Armor, index = 0, parts = PartCatalog.Armors, current = loadout.armor });
                break;
            case Tab.Weapons:
                for (int i = 0; i < Craft().weapons; i++)
                    list.Add(MakeSlot($"Silah {i + 1}  •  {(i < 2 ? "gövde" : "kanat")}", SlotKind.Weapon, i, PartCatalog.Weapons, loadout.weapons[i]));
                break;
            case Tab.Missiles:
                string[] pylon = { "sol kanat altı", "sağ kanat altı" };
                for (int i = 0; i < Craft().missiles; i++)
                    list.Add(MakeSlot($"Füze {i + 1}  •  {pylon[i]}", SlotKind.Missile, i, PartCatalog.Missiles, loadout.missiles[i]));
                break;
            case Tab.Shields:
                for (int i = 0; i < Craft().shields; i++)
                    list.Add(MakeSlot($"Jeneratör {i + 1}", SlotKind.Shield, i, PartCatalog.Shields, loadout.shields[i]));
                break;
            default:
                string[] mount = { "sırt (üst)", "karın (alt)" };
                for (int i = 0; i < Craft().turrets; i++)
                    list.Add(MakeSlot($"Taret {i + 1}  •  {mount[i]}", SlotKind.Turret, i, PartCatalog.Turrets, loadout.turrets[i]));
                break;
        }
        return list;
    }

    private AircraftDef Craft() { return AircraftCatalog.Get(model); }

    private Slot MakeSlot(string title, SlotKind kind, int index, PartDef[] parts, int current)
    {
        bool locked = !Unlocks.IsOpen(model, kind, index);
        return new Slot
        {
            title = title, kind = kind, index = index, parts = parts,
            current = locked ? 0 : current,
            locked = locked,
            requirement = locked ? Unlocks.SlotRequirement(model, kind, index) : ""
        };
    }

    // Sekme icin: bu ucakta o kategorideki kilitli slot sayisi
    private int LockedCount(Tab t)
    {
        SlotKind kind;
        switch (t)
        {
            case Tab.Weapons: kind = SlotKind.Weapon; break;
            case Tab.Missiles: kind = SlotKind.Missile; break;
            case Tab.Shields: kind = SlotKind.Shield; break;
            case Tab.Turrets: kind = SlotKind.Turret; break;
            default: return 0;
        }

        int n = 0;
        int count = Craft().SlotCount(kind);
        for (int i = 0; i < count; i++)
            if (!Unlocks.IsOpen(model, kind, i)) n++;
        return n;
    }

    private int FilledCount(Tab t)
    {
        switch (t)
        {
            case Tab.Body: return 3;
            case Tab.Weapons: return CountNonZero(loadout.weapons);
            case Tab.Missiles: return CountNonZero(loadout.missiles);
            case Tab.Shields: return CountNonZero(loadout.shields);
            default: return CountNonZero(loadout.turrets);
        }
    }

    private int SlotCount(Tab t)
    {
        switch (t)
        {
            case Tab.Body: return 3;
            case Tab.Weapons: return Craft().weapons;
            case Tab.Missiles: return Craft().missiles;
            case Tab.Shields: return Craft().shields;
            default: return Craft().turrets;
        }
    }

    private static int CountNonZero(int[] a)
    {
        int n = 0;
        foreach (int v in a) if (v > 0) n++;
        return n;
    }

    // Kart ustunde okunacak kisa etki ozeti
    private static string Summary(SlotKind kind, PartDef d, int index)
    {
        if (index == 0 && (kind == SlotKind.Weapon || kind == SlotKind.Missile || kind == SlotKind.Shield || kind == SlotKind.Turret))
            return "Boş  •  ▶ ile parça tak";

        switch (kind)
        {
            case SlotKind.Engine:
                return $"Güç kapasitesi {d.power:0}  •  itki x{d.thrust:0.00}  •  ağırlık {Signed(d.weight)}";
            case SlotKind.Wings:
                return $"Yatış x{d.roll:0.00}  •  seyir hızı x{d.cruise:0.00}  •  ağırlık {Signed(d.weight)}";
            case SlotKind.Armor:
                return $"{d.hp:0} can  •  ağırlık {Signed(d.weight)}";
            case SlotKind.Weapon:
                return $"{d.rate * d.damage:0} hasar/sn  •  güç {d.power:0}  •  ağırlık {Signed(d.weight)}";
            case SlotKind.Missile:
                return $"{d.damage:0} hasar  •  {d.ammo} füze  •  güç {d.power:0}";
            case SlotKind.Shield:
                return $"+{d.shield:0} kalkan  •  {d.regen:0.0}/sn  •  güç {d.power:0}";
            default:
                return $"{d.rate * d.damage:0} hasar/sn  •  {d.range:0} m  •  güç {d.power:0}";
        }
    }

    private static string Signed(float v)
    {
        return v > 0.001f ? "+" + v.ToString("0") : v.ToString("0");
    }

    // ------------------------------------------------------------------ GUI

    private void OnGUI()
    {
        Hud.Begin();
        float W = Hud.W, H = Hud.H;

        DrawTopBar(W);
        DrawLeft(H);
        DrawRight(W, H);
        DrawBottom(W, H);

        if (showHelp) DrawHelp(W, H);

        if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil)
        {
            Rect t = new Rect(W * 0.5f - 340f, 76f, 680f, 40f);
            Hud.Fill(t, new Color(0f, 0f, 0f, 0.82f));
            Hud.Text(t, toast, new Color(1f, 0.6f, 0.35f), 16, TextAnchor.MiddleCenter);
        }

        Hud.End();
    }

    // Tiklanabilir duz dugme (secili / uzerine gelinen durumlu)
    private bool Btn(Rect r, string text, int size, bool selected = false, Color? accent = null)
    {
        bool hover = r.Contains(Event.current.mousePosition) && !showHelp;
        Color a = accent ?? new Color(0.3f, 0.6f, 1f);

        Color bg = selected ? new Color(a.r * 0.5f, a.g * 0.5f, a.b * 0.5f, 0.95f)
            : hover ? new Color(0.22f, 0.28f, 0.38f, 0.95f)
            : new Color(0.1f, 0.13f, 0.19f, 0.92f);
        Hud.Fill(r, bg);
        if (selected) Hud.Fill(new Rect(r.x, r.yMax - 3f, r.width, 3f), a);

        Hud.Text(r, text, Color.white, size, TextAnchor.MiddleCenter);
        return !showHelp && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    private void DrawTopBar(float W)
    {
        Hud.Fill(new Rect(0f, 0f, W, TopH), new Color(0.03f, 0.05f, 0.08f, 0.85f));
        Hud.Text(new Rect(Gap + 4f, 12f, 220f, 40f), "HANGAR", Color.white, 30, TextAnchor.MiddleLeft);

        // Ucak sekmeleri (kilitli ucaklar sonu kilit isaretiyle gorunur; secilip incelenebilir)
        string[] names = PartCatalog.ModelNames;
        float gap = 4f;
        float x = 244f;      // "HANGAR" basligi ile boya secicisi arasinda
        float bh = 42f;
        // Sekme genisligi ucak sayisina gore daralir (boya secicisine tasmasin)
        float bw = Mathf.Min(108f, (W - Gap - 330f - 8f - x) / names.Length - gap);
        for (int i = 0; i < names.Length; i++)
        {
            bool owned = Unlocks.OwnsAircraft(i);
            string label = ShortName(names[i]);
            Rect r = new Rect(x + i * (bw + gap), 11f, bw, bh);

            if (Btn(r, label, bw < 110f ? 11 : 13, i == model, owned ? Gold : new Color(1f, 0.4f, 0.35f)) && i != model)
                lastDesc = CycleModel(i);

            if (!owned)
            {
                // Kucuk kilit: govde + kulp
                Color red = new Color(1f, 0.45f, 0.4f);
                Hud.Fill(new Rect(r.xMax - 18f, r.y + 8f, 10f, 8f), red);
                Hud.Outline(new Rect(r.xMax - 17f, r.y + 3f, 8f, 7f), red, 2f);
            }
        }

        // Boya
        float px = W - Gap - 330f;
        Hud.Text(new Rect(px, 12f, 50f, 40f), "BOYA", Dim, 13, TextAnchor.MiddleLeft);
        if (Btn(new Rect(px + 52f, 15f, 34f, 34f), "◀", 14)) lastDesc = CyclePaint(-1);
        Hud.Text(new Rect(px + 90f, 15f, 124f, 34f), PartCatalog.Paints[loadout.paint].name, Color.white, 15, TextAnchor.MiddleCenter);
        if (Btn(new Rect(px + 218f, 15f, 34f, 34f), "▶", 14)) lastDesc = CyclePaint(1);

        if (Btn(new Rect(px + 262f, 15f, 68f, 34f), "NASIL?", 13)) showHelp = true;
    }

    private void DrawLeft(float H)
    {
        float x = Gap, y = TopH + 8f, w = LeftW;
        float h = H - y - BottomH - 4f;
        Hud.Panel(new Rect(x, y, w, h), 0.78f);

        // Kategori sekmeleri
        float tw = (w - 12f) / TabNames.Length;
        for (int i = 0; i < TabNames.Length; i++)
        {
            Tab t = (Tab)i;
            Rect r = new Rect(x + 6f + i * tw, y + 6f, tw - 3f, 40f);
            int locked = LockedCount(t);
            int total = SlotCount(t);
            int openSlots = total - locked;
            string label = total == 0 ? $"{TabNames[i]}\nyok"
                : (openSlots > 0 ? $"{TabNames[i]}\n{FilledCount(t)}/{openSlots}" : $"{TabNames[i]}\nkilitli");
            if (Btn(r, label, 12, tab == t, TabColors[i])) tab = t;

            // Kilitli slotu olan sekmede kucuk kirmizi isaret
            if (locked > 0) Hud.Fill(new Rect(r.xMax - 12f, r.y + 4f, 8f, 8f), new Color(1f, 0.35f, 0.3f));
        }

        // Slot kartlari
        List<Slot> slots = BuildSlots();
        float cy = y + 56f;
        const float cardH = 84f;

        string hoverDesc = null;

        if (!Unlocks.OwnsAircraft(model))
        {
            // Kilitli ucak: donanim kurulamaz, onizleme ve ozellikler incelenebilir
            Rect banner = new Rect(x + 8f, cy, w - 16f, 200f);
            Hud.Fill(banner, new Color(0.1f, 0.06f, 0.07f, 0.95f));
            Hud.Fill(new Rect(banner.x, banner.y, 5f, banner.height), new Color(1f, 0.45f, 0.4f, 0.8f));
            Hud.Text(new Rect(banner.x + 20f, banner.y + 14f, banner.width - 40f, 30f), "BU UÇAK KİLİTLİ", new Color(1f, 0.5f, 0.45f), 22);
            Wrapped(new Rect(banner.x + 20f, banner.y + 52f, banner.width - 40f, 60f), "Açmak için: " + Unlocks.AircraftRequirement(model),
                new Color(1f, 0.92f, 0.65f), 16);
            AircraftDef a = Craft();
            Wrapped(new Rect(banner.x + 20f, banner.y + 112f, banner.width - 40f, 80f),
                $"{a.tagline}\nSlot: {a.weapons} silah, {a.missiles} füze, {a.shields} kalkan, {a.turrets} taret   •   Can çarpanı x{a.hpMul:0.##}",
                Dim, 13);
        }
        else
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Slot s = slots[i];
                Rect card = new Rect(x + 8f, cy, w - 16f, cardH);
                DrawSlotCard(card, s, TabColors[(int)tab], ref hoverDesc);
                cy += cardH + 6f;
            }

            if (slots.Count == 0)
                Wrapped(new Rect(x + 16f, cy + 10f, w - 32f, 60f), "Bu uçakta bu kategoride slot yok.", Dim, 15);
        }

        // Parca ayrintisi
        Rect detail = new Rect(x + 8f, y + h - 124f, w - 16f, 116f);
        Hud.Fill(detail, new Color(0f, 0f, 0f, 0.35f));
        Hud.Text(new Rect(detail.x + 10f, detail.y + 6f, detail.width - 20f, 18f), "PARÇA AYRINTISI", Gold, 12);
        Hud.Text(new Rect(detail.x + 10f, detail.y + 6f, detail.width - 20f, 18f),
            $"Açılan slot {Unlocks.OpenRuleCount(model)}/{Unlocks.RuleCount(model)}  •  boss yenince yenisi açılır",
            Dim, 11, TextAnchor.UpperRight, false);

        var wrap = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, alignment = TextAnchor.UpperLeft };
        wrap.normal.textColor = new Color(0.9f, 0.93f, 0.97f);
        GUI.Label(new Rect(detail.x + 10f, detail.y + 26f, detail.width - 20f, detail.height - 30f),
            string.IsNullOrEmpty(hoverDesc) ? lastDesc : hoverDesc, wrap);
    }

    private void DrawSlotCard(Rect r, Slot s, Color accent, ref string hoverDesc)
    {
        if (s.locked)
        {
            DrawLockedCard(r, s, ref hoverDesc);
            return;
        }

        PartDef part = s.parts[s.current];
        bool empty = s.current == 0 && s.kind != SlotKind.Engine && s.kind != SlotKind.Wings && s.kind != SlotKind.Armor;
        bool hover = r.Contains(Event.current.mousePosition) && !showHelp;

        Hud.Fill(r, hover ? new Color(0.16f, 0.21f, 0.3f, 0.97f) : new Color(0.09f, 0.12f, 0.18f, 0.95f));
        Hud.Fill(new Rect(r.x, r.y, 5f, r.height), empty ? new Color(1f, 1f, 1f, 0.15f) : accent);

        Hud.Text(new Rect(r.x + 56f, r.y + 7f, r.width - 112f, 16f), s.title, Dim, 12, TextAnchor.UpperLeft, false);
        Hud.Text(new Rect(r.x + 56f, r.y + 24f, r.width - 112f, 28f), part.name,
            empty ? new Color(1f, 1f, 1f, 0.45f) : Color.white, 20, TextAnchor.UpperLeft);
        Hud.Text(new Rect(r.x + 56f, r.y + 56f, r.width - 112f, 24f), Summary(s.kind, part, s.current),
            empty ? Dim : new Color(1f, 0.92f, 0.65f), 12, TextAnchor.UpperLeft, false);

        if (hover) hoverDesc = part.description;

        // Ok dugmeleri
        Rect left = new Rect(r.x + 8f, r.y + 10f, 40f, r.height - 20f);
        Rect right = new Rect(r.xMax - 48f, r.y + 10f, 40f, r.height - 20f);
        if (Btn(left, "◀", 18)) lastDesc = CycleSlot(s, -1);
        if (Btn(right, "▶", 18)) lastDesc = CycleSlot(s, 1);

        // Kartin ortasina tiklamak da ileri alir
        Rect mid = new Rect(r.x + 52f, r.y, r.width - 104f, r.height);
        if (!showHelp && GUI.Button(mid, GUIContent.none, GUIStyle.none)) lastDesc = CycleSlot(s, 1);
    }

    // Kilitli slot: sonuc kosulunu gosterir, tiklanamaz
    private void DrawLockedCard(Rect r, Slot s, ref string hoverDesc)
    {
        bool hover = r.Contains(Event.current.mousePosition) && !showHelp;
        Color red = new Color(1f, 0.45f, 0.4f);

        Hud.Fill(r, new Color(0.07f, 0.08f, 0.1f, 0.95f));
        Hud.Fill(new Rect(r.x, r.y, 5f, r.height), new Color(red.r, red.g, red.b, 0.7f));

        // Asma kilit isareti: govde + kulp
        float lx = r.x + 20f, ly = r.y + 30f;
        Hud.Outline(new Rect(lx + 5f, ly - 10f, 16f, 14f), red, 3f);
        Hud.Fill(new Rect(lx, ly + 2f, 26f, 20f), red);

        Hud.Text(new Rect(r.x + 64f, r.y + 7f, r.width - 76f, 16f), s.title, Dim, 12, TextAnchor.UpperLeft, false);
        Hud.Text(new Rect(r.x + 64f, r.y + 24f, r.width - 76f, 28f), "KİLİTLİ", red, 20, TextAnchor.UpperLeft);
        Hud.Text(new Rect(r.x + 64f, r.y + 54f, r.width - 76f, 30f), s.requirement,
            new Color(1f, 0.92f, 0.65f), 12, TextAnchor.UpperLeft, false);

        if (hover) hoverDesc = "Bu slot kilitli. " + s.requirement + ". Boss yenilince slot açılır ve varsayılan parça otomatik takılır.";
    }

    private void DrawRight(float W, float H)
    {
        float x = W - Gap - RightW, y = TopH + 8f, w = RightW;
        float h = H - y - BottomH - 4f;
        Hud.Panel(new Rect(x, y, w, h), 0.78f);

        LoadoutStats st = cur;
        float cy = y + 10f;

        // ---- Ucus
        Hud.Text(new Rect(x + 14f, cy, w - 28f, 20f), "UÇUŞ PERFORMANSI", Gold, 14);
        cy += 26f;
        // Taban puan = ucagin kendi ozelligi (jetler ust siralarda); parcalar bunu carpar
        PlaneController.PresetData pd = PlaneController.DataOf(AircraftCatalog.Get(model).preset);
        Rating(x + 14f, ref cy, w - 28f, "Hız", st.EffMaxSpeed, prev.EffMaxSpeed, pd.maxSpeed / 25f);
        Rating(x + 14f, ref cy, w - 28f, "İvme", st.EffThrust, prev.EffThrust, pd.thrust / 4.6f);
        Rating(x + 14f, ref cy, w - 28f, "Çeviklik (yatış)", st.EffRoll, prev.EffRoll, pd.roll / 58f);
        Rating(x + 14f, ref cy, w - 28f, "Dönüş gücü (G)", st.EffMaxG, prev.EffMaxG, pd.maxG / 2.0f);
        Hud.Text(new Rect(x + 14f, cy - 2f, w - 28f, 16f), "10 üzerinden; ağır donanım puanı düşürür.", Dim, 11, TextAnchor.UpperLeft, false);
        cy += 26f;

        // ---- Savas
        Hud.Text(new Rect(x + 14f, cy, w - 28f, 20f), "SAVAŞ GÜCÜ", Gold, 14);
        cy += 26f;

        float tileW = (w - 28f - 16f) / 3f, tileH = 66f;
        float tx = x + 14f;
        Tile(new Rect(tx, cy, tileW, tileH), "CAN", $"{st.hp:0}", st.hp - prev.hp, new Color(0.4f, 0.95f, 0.5f));
        Tile(new Rect(tx + (tileW + 8f), cy, tileW, tileH), "KALKAN", $"{st.shieldMax:0}", st.shieldMax - prev.shieldMax, new Color(0.4f, 0.8f, 1f));
        Tile(new Rect(tx + (tileW + 8f) * 2f, cy, tileW, tileH), "FÜZE", $"{st.missileRounds}", st.missileRounds - prev.missileRounds, new Color(1f, 0.55f, 0.45f));
        cy += tileH + 8f;
        Tile(new Rect(tx, cy, tileW, tileH), "SİLAH /sn", $"{st.weaponDps:0}", st.weaponDps - prev.weaponDps, new Color(1f, 0.7f, 0.3f));
        Tile(new Rect(tx + (tileW + 8f), cy, tileW, tileH), "TARET /sn", $"{st.turretDps:0}", st.turretDps - prev.turretDps, new Color(0.8f, 0.65f, 1f));
        Tile(new Rect(tx + (tileW + 8f) * 2f, cy, tileW, tileH), "AĞIRLIK", $"{st.weight:0}", st.weight - prev.weight, Color.white, true);
        cy += tileH + 16f;

        // ---- Guc
        Hud.Text(new Rect(x + 14f, cy, w - 28f, 20f), "GÜÇ BÜTÇESİ", Gold, 14);
        cy += 26f;

        int cap = Mathf.Max(1, Mathf.RoundToInt(st.powerCap));
        int used = Mathf.RoundToInt(st.powerUsed);
        float cellGap = 3f;
        float cellW = (w - 28f - cellGap * (cap - 1)) / cap;
        for (int i = 0; i < cap; i++)
        {
            Color c = i < used
                ? (used >= cap ? new Color(1f, 0.7f, 0.25f) : new Color(0.35f, 0.65f, 1f))
                : new Color(1f, 1f, 1f, 0.12f);
            Hud.Fill(new Rect(x + 14f + i * (cellW + cellGap), cy, cellW, 20f), c);
        }
        cy += 26f;

        int free = cap - used;
        string powerText = free <= 0 ? $"{used} / {cap}  •  bütçe dolu" : $"{used} / {cap}  •  {free} boş";
        Hud.Text(new Rect(x + 14f, cy, w - 28f, 20f), powerText, Color.white, 15, TextAnchor.UpperLeft);
        cy += 22f;
        Wrapped(new Rect(x + 14f, cy, w - 28f, 44f),
            "Silah, füze, kalkan ve taretler güç harcar. Daha çok güç için güçlü bir motor tak.", Dim, 12);
    }

    // Sarmali kucuk metin (Hud.Text sarmaz)
    private static void Wrapped(Rect r, string text, Color color, int size)
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, alignment = TextAnchor.UpperLeft };
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }

    // Puan cubugu (0-10): 5 = standart donanim. Son degisikligin farki yanda gorunur.
    private void Rating(float x, ref float y, float w, string label, float eff, float prevEff, float baseScore)
    {
        float score = Mathf.Clamp(eff * baseScore, 0f, 10f);
        float prevScore = Mathf.Clamp(prevEff * baseScore, 0f, 10f);
        float delta = score - prevScore;

        Hud.Text(new Rect(x, y, w * 0.6f, 18f), label, Color.white, 14, TextAnchor.UpperLeft, false);
        Hud.Text(new Rect(x + w - 150f, y, 60f, 18f), score.ToString("0.0"), Color.white, 15, TextAnchor.UpperRight);
        if (hasPrev && Mathf.Abs(delta) >= 0.05f)
        {
            Color dc = delta > 0f ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.45f, 0.4f);
            Hud.Text(new Rect(x + w - 86f, y, 86f, 18f), (delta > 0f ? "▲ +" : "▼ ") + delta.ToString("0.0"), dc, 14, TextAnchor.UpperRight);
        }
        y += 20f;

        Rect bar = new Rect(x, y, w, 9f);
        Hud.Fill(bar, new Color(1f, 1f, 1f, 0.1f));
        Hud.Fill(new Rect(bar.x, bar.y, bar.width * (score / 10f), bar.height),
            score >= 5f ? new Color(0.35f, 0.85f, 0.5f) : new Color(1f, 0.62f, 0.25f));
        Hud.Fill(new Rect(bar.x + bar.width * 0.5f - 1f, bar.y - 3f, 2f, bar.height + 6f), Color.white);   // standart isareti
        y += 17f;
    }

    private void Tile(Rect r, string label, string value, float delta, Color color, bool lowerIsBetter = false)
    {
        Hud.Fill(r, new Color(0f, 0f, 0f, 0.35f));
        Hud.Fill(new Rect(r.x, r.y, r.width, 3f), color);

        Hud.Text(new Rect(r.x, r.y + 6f, r.width, 30f), value, Color.white, 24, TextAnchor.MiddleCenter);
        Hud.Text(new Rect(r.x, r.y + 36f, r.width, 14f), label, Dim, 11, TextAnchor.MiddleCenter, false);

        if (hasPrev && Mathf.Abs(delta) >= 0.05f)
        {
            bool good = lowerIsBetter ? delta < 0f : delta > 0f;
            Color dc = good ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.5f, 0.4f);
            string t = (delta > 0f ? "▲ +" : "▼ ") + delta.ToString("0.#");
            Hud.Text(new Rect(r.x, r.y + 50f, r.width, 14f), t, dc, 12, TextAnchor.MiddleCenter);
        }
    }

    private void DrawBottom(float W, float H)
    {
        Hud.Fill(new Rect(0f, H - BottomH, W, BottomH), new Color(0.03f, 0.05f, 0.08f, 0.85f));

        float y = H - BottomH + 22f;

        bool resume = RunState.HasPendingCampaign;
        string goal = resume
            ? $"Operasyon devam ediyor: AŞAMA {RunState.PendingStage + 1} sırada  •  skor {RunState.Score:N0}  •  donanımı değiştirip devam et"
            : "HEDEF:  " + Stages.All.Length + " aşamayı geç  •  düşman üssünü bas: komuta merkezini yık  •  rozetli düşmanı vur  •  B: bomba  •  boss'u indir";
        bool owned = Unlocks.OwnsAircraft(model);

        int stageIdx = resume ? Mathf.Clamp(RunState.PendingStage, 0, Stages.All.Length - 1) : 0;
        float needed = Stages.All[stageIdx].minFirepower;
        float firepower = cur.weaponDps + cur.turretDps;
        if (owned && firepower >= needed) Hud.Text(new Rect(0f, H - BottomH + 2f, W, 20f), goal, resume ? Gold : Dim, 13, TextAnchor.MiddleCenter, false);

        float bw = 380f, bh = 56f;
        string startLabel = resume ? $"AŞAMA {RunState.PendingStage + 1}'E DEVAM" : "SAVAŞA BAŞLA";
        Rect startRect = new Rect(W * 0.5f - bw * 0.5f, y, bw, bh);

        if (!owned)
        {
            // Kilitli ucakla savasa girilmez; sartini yaz
            Hud.Fill(startRect, new Color(0.12f, 0.14f, 0.18f, 0.95f));
            Hud.Text(startRect, "UÇAK KİLİTLİ", new Color(1f, 0.5f, 0.45f), 22, TextAnchor.MiddleCenter);
            Hud.Text(new Rect(0f, H - BottomH + 2f, W, 20f), "Açmak için: " + Unlocks.AircraftRequirement(model),
                new Color(1f, 0.82f, 0.5f), 13, TextAnchor.MiddleCenter, false);
        }
        else if (firepower < needed)
        {
            // Silahsiz ya da gereken ates gucunun altindaki donanimla savasa girilmez
            Hud.Fill(startRect, new Color(0.14f, 0.12f, 0.1f, 0.95f));
            Hud.Text(startRect, "ATEŞ GÜCÜ YETERSİZ", new Color(1f, 0.55f, 0.4f), 22, TextAnchor.MiddleCenter);
            string why = firepower <= 0.01f
                ? "Silahsız savaşa çıkılamaz: en az bir silah tak."
                : $"AŞAMA {stageIdx + 1} için en az {needed:0} ateş gücü (SİLAH + TARET /sn) gerek, şu an {firepower:0}.";
            Hud.Text(new Rect(0f, H - BottomH + 2f, W, 20f), why, new Color(1f, 0.82f, 0.5f), 13, TextAnchor.MiddleCenter, false);
        }
        else if (Btn(startRect, startLabel, 26, true, new Color(0.3f, 0.85f, 0.4f)))
        {
            LoadoutStore.SelectedModel = model;
            LoadoutStore.Save();
            SceneManager.LoadScene(GameScenes.Flight);
        }

        if (resume && Btn(new Rect(W * 0.5f + bw * 0.5f + 20f, y + 8f, 140f, 40f), "YENİ OYUN", 15))
        {
            RunState.CancelPending();
            Toast("Yeni oyun: 1. aşamadan başlayacaksın.");
        }

        if (Btn(new Rect(W * 0.5f - bw * 0.5f - 160f, y + 8f, 140f, 40f), "SIFIRLA", 15))
        {
            LoadoutStore.Reset(model);
            RebuildPreview();
            lastDesc = "Bu uçağın donanımı varsayılana döndürüldü.";
        }

        float exitX = W * 0.5f + bw * 0.5f + 20f + (resume ? 150f : 0f);
        if (Btn(new Rect(exitX, y + 8f, 140f, 40f), "ÇIKIŞ", 15))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        Hud.Text(new Rect(Gap + LeftW + Gap, H - BottomH - 24f, W - 2f * (Gap + LeftW + Gap), 18f),
            "Sürükle: döndür  •  Tekerlek: yakınlaştır", new Color(0.8f, 0.85f, 0.9f, 0.8f), 12, TextAnchor.MiddleCenter, false);
    }

    // Nasil oynanir: hedef, kontroller, ilerleme
    private void DrawHelp(float W, float H)
    {
        Hud.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.8f));

        float pw = 760f, ph = 470f;
        Rect p = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
        Hud.Fill(p, new Color(0.06f, 0.09f, 0.14f, 0.98f));
        Hud.Fill(new Rect(p.x, p.y, pw, 5f), Gold);

        Hud.Text(new Rect(p.x, p.y + 14f, pw, 40f), "NASIL OYNANIR", Color.white, 28, TextAnchor.MiddleCenter);

        string[] heads = { "HEDEF", "KONTROLLER", "GÜÇLENME" };
        string[] body =
        {
            Stages.All.Length + " aşamayı geç. Her aşamada bir düşman üssü var (kıyı, ada, kale ve tapınak üsleri): uçaksavarları, radarı, hangarları ve yakıt depolarını vur, KOMUTA MERKEZİNİ yık; yıkılınca uçaksavarlar susar ve ana boss gelir. Aşamalar arasında denizi geçersin. Ortada bir orta boss, sonda ana boss seni bekler (köpekbalığı, ahtapot, ejderha, ki savaşçısı, yıldız büyücüsü...). Uçağın düşerse oyun biter. Savaşa girmek için donanımının asgari ateş gücüne (silah + taret /sn) sahip olması gerekir.",
            "FARE  yönlendir        SOL TIK / SPACE  ateş        F  güdümlü füze        B  BOMBA\nW/S  ileri / geri        A/D  sağa-sola kay        SHIFT/CTRL  yukarı-aşağı kay        SAĞ TIK  yakınlaştır\nESC  duraklat, mağaza ve kontrol şeması (Fare / Arcade / Klasik)        H  kontrol ipucu",
            "Rozetli (P, S, M, B, +) düşmanı vur: güçlendirme düşürür, üstünden geçerek topla. Bomba ekrandaki mermileri temizler, bosslara yüzde hasar verir. Başlangıçta yalnızca 2 gövde silahın açık; her boss'u ilk kez yenince yeni bir slot (füze, kalkan, taret, kanat silahı) açılır ve otomatik takılır. Hurda ile mağazadan yükseltme alırsın."
        };

        float y = p.y + 66f;
        float[] hs = { 76f, 82f, 100f };
        var wrap = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        wrap.normal.textColor = new Color(0.9f, 0.93f, 0.97f);

        for (int i = 0; i < heads.Length; i++)
        {
            Hud.Text(new Rect(p.x + 30f, y, pw - 60f, 20f), heads[i], Gold, 14);
            GUI.Label(new Rect(p.x + 30f, y + 22f, pw - 60f, hs[i]), body[i], wrap);
            y += hs[i] + 30f;
        }

        Rect close = new Rect(p.center.x - 110f, p.yMax - 62f, 220f, 44f);
        Hud.Fill(close, new Color(0.2f, 0.5f, 0.3f, 0.95f));
        Hud.Text(close, "ANLADIM", Color.white, 18, TextAnchor.MiddleCenter);
        if (GUI.Button(close, GUIContent.none, GUIStyle.none)) showHelp = false;
    }
}
