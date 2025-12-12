using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using Bulbmaps;

public class BulbmapsScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public GameObject[] BulbObjs;
    public Material[] BulbMats;
    public Light[] BulbLights;

    public GameObject GridParent;
    public GameObject DoorObj;

    public KMSelectable SwitchSel;
    public GameObject SwitchCasing;
    public KMSelectable[] ButtonSels;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private int _currentMap;
    private bool _isAnimating;

    private BulbColor _mostCommonColor;

    public enum BulbColor
    {
        Red,
        Yellow,
        Green,
        Blue,
        Purple,
        White
    }

    private static readonly Color32[] _lightColors = new Color32[] {
        new Color32(255, 80, 80, 255),      // red
        new Color32(255, 255, 80, 255),     // yellow
        new Color32(80, 255, 80, 255),      // green
        new Color32(100, 120, 255, 255),    // blue
        new Color32(255, 80, 255, 255),     // purple
        new Color32(200, 200, 200, 255)     // white
    };

    public class BulbInfo
    {
        public BulbColor Color;
        public bool IsLit;
        public bool IsTransparent;

        public BulbInfo(BulbColor color, bool light, bool transparency)
        {
            Color = color;
            IsLit = light;
            IsTransparent = transparency;
        }
    }

    public BulbInfo[][] _bulbMapInfo = new BulbInfo[2][] { new BulbInfo[36], new BulbInfo[36] };

    private static readonly string[] _rules = new string[]
    {
        "Transparent green bulbs",
        "Lit white bulbs",
        "Bulbs in the same position across both maps with a matching transparency",
        "Bulbs that have four transparent bulbs orthogonally adjacent to it",
        "Unlit white bulbs",
        "Unlit yellow bulbs",
        "2×2 section of bulbs with matching colors",
        "Opaque blue bulbs",
        "Opaque red bulbs",
        "Opaque green bulbs",
        "Transparent white bulbs",
        "Transparent red bulbs",
        "Transparent blue bulbs",
        "Bulbs that have four opaque bulbs orthogonally adjacent to it",
        "Transparent yellow bulbs",
        "Bulbs that have four unlit bulbs orthogonally adjacent to it",
        "Lit red bulbs",
        "Opaque white bulbs",
        "Bulbs in the same position across both maps with a matching color",
        "Unlit red bulbs",
        "Opaque purple bulbs",
        "Columns or rows with an equal count of transparent and opaque bulbs",
        "Unlit purple bulbs",
        "2×2 section of bulbs with matching lit state",
        "Lit green bulbs",
        "Lit blue bulbs",
        "Lit yellow bulbs",
        "Columns or rows with an equal count of lit and unlit bulbs",
        "Opaque yellow bulbs",
        "Unlit blue bulbs",
        "Bulbs in the same position across both maps with a matching lit state",
        "Unlit green bulbs",
        "Bulbs with a matching colored bulb orthogonally adjacent to it",
        "Lit purple bulbs",
        "Transparent purple bulbs",
        "Bulbs that have four lit bulbs orthogonally adjacent to it",
        "2×2 section of bulbs with matching transparency",
    };

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        SwitchSel.OnInteract += SwitchPress;
        for (int i = 0; i < ButtonSels.Length; i++)
            ButtonSels[i].OnInteract += ButtonPress(i);
        foreach (var l in BulbLights)
            l.range *= transform.lossyScale.x;

        for (int map = 0; map < 2; map++)
        {
            for (int i = 0; i < 36; i++)
            {
                var c = (BulbColor)Rnd.Range(0, 6);
                var l = Rnd.Range(0, 2) == 0;
                var t = Rnd.Range(0, 2) == 0;
                _bulbMapInfo[map][i] = new BulbInfo(c, l, t);
            }
        }

        var colorCounts = Enum.GetValues(typeof(BulbColor))
            .Cast<BulbColor>()
            .ToDictionary(c => c, c => 0);

        for (int map = 0; map < 2; map++)
            for (int i = 0; i < 36; i++)
                colorCounts[_bulbMapInfo[map][i].Color]++;

        int maxCount = colorCounts.Values.Max();

        var tiedColors = colorCounts
            .Where(kv => kv.Value == maxCount)
            .Select(kv => kv.Key)
            .ToList();

        if (tiedColors.Count > 1)
        {
            BulbColor winnerColor = tiedColors[Rnd.Range(0, tiedColors.Count)];
            _mostCommonColor = winnerColor;
            for (int map = 0; map < 2; map++)
            {
                for (int i = 0; i < 36; i++)
                {
                    if (_bulbMapInfo[map][i].Color != winnerColor)
                    {
                        _bulbMapInfo[map][i].Color = winnerColor;
                        goto Done;
                    }
                }
            }
        }
        else
        {
            _mostCommonColor = tiedColors[0];
        }
        Done:;
        LogBulbmaps();

        Debug.LogFormat("[Bulbmaps #{0}] The most common occurring color is {1}.", _moduleId, _mostCommonColor);

        Calculate();

        SetBulbs(_currentMap);
        SetBulbLights(_currentMap);
    }

    private KMSelectable.OnInteractHandler ButtonPress(int btn)
    {
        return delegate ()
        {
            if (_moduleSolved)
                return false;
            return false;
        };
    }

    private bool SwitchPress()
    {
        if (_isAnimating)
            return false;
        StartCoroutine(FlipSwitch());
        StartCoroutine(ToggleBulbs());
        return false;
    }

    private void LogBulbmaps()
    {
        for (int map = 0; map < 2; map++)
        {
            Debug.LogFormat("[Bulbmaps #{0}] Bulbmap on the {1} page:", _moduleId, map == 0 ? "O" : "I");
            for (int row = 0; row < 6; row++)
            {
                var colors = Enumerable.Range(0, 6).Select(col => "RYGBPW"[(int)_bulbMapInfo[map][col + (6 * row)].Color]).Join("");
                var transparents = Enumerable.Range(0, 6).Select(col => _bulbMapInfo[map][col + (6 * row)].IsTransparent ? "t" : "o").Join("");
                var lightStates = Enumerable.Range(0, 6).Select(col => _bulbMapInfo[map][col + (6 * row)].IsLit ? "1" : "0").Join("");
                Debug.LogFormat("[Bulbmaps #{0}] {1}", _moduleId, Enumerable.Range(0, 6).Select(i => colors[i].ToString() + transparents[i].ToString() + lightStates[i].ToString()).Join(" "));
            }
        }
    }

    private void SetBulbs(int map)
    {
        for (int i = 0; i < 36; i++)
        {
            BulbObjs[i].GetComponent<MeshRenderer>().material =
                BulbMats[(int)_bulbMapInfo[map][i].Color + (_bulbMapInfo[map][i].IsTransparent ? 6 : 0)];
            BulbLights[i].color = _lightColors[(int)_bulbMapInfo[map][i].Color];
        }
    }

    private void SetBulbLights(int map)
    {
        for (int i = 0; i < 36; i++)
            BulbLights[i].enabled = _bulbMapInfo[map][i].IsLit;
    }

    private IEnumerator FlipSwitch()
    {
        var duration = 0.1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SwitchCasing.transform.localEulerAngles = new Vector3(180f, 0f, Easing.InOutQuad(elapsed, (_currentMap % 2) * 30f, (_currentMap + 1) % 2 * 30f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        SwitchCasing.transform.localEulerAngles = new Vector3(180f, 0f, (_currentMap + 1) % 2 * 30f);
    }

    private IEnumerator ToggleBulbs()
    {
        _isAnimating = true;
        foreach (var l in BulbLights)
            l.enabled = false;
        yield return new WaitForSeconds(0.1f);
        var duration = 0.2f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            GridParent.transform.localPosition = new Vector3(0f, Easing.InOutQuad(elapsed, 0f, -0.0175f, duration), 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        elapsed = 0f;
        while (elapsed < duration)
        {
            DoorObj.transform.localPosition = new Vector3(0f, 0f, Easing.InOutQuad(elapsed, 0.1f, 0f, duration));
            DoorObj.transform.localScale = new Vector3(1f, 1f, Easing.InOutQuad(elapsed, 0f, 1f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        _currentMap = (_currentMap + 1) % 2;
        yield return null;
        SetBulbs(_currentMap);
        yield return new WaitForSeconds(0.25f);
        elapsed = 0f;
        while (elapsed < duration)
        {
            DoorObj.transform.localScale = new Vector3(1f, 1f, Easing.InOutQuad(elapsed, 1f, 0f, duration));
            DoorObj.transform.localPosition = new Vector3(0f, 0f, Easing.InOutQuad(elapsed, 0f, 0.1f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        DoorObj.transform.localScale = new Vector3(1f, 1f, 0f);
        DoorObj.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        elapsed = 0f;
        while (elapsed < duration)
        {
            GridParent.transform.localPosition = new Vector3(0f, Easing.InOutQuad(elapsed, -0.0175f, 0f, duration), 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        GridParent.transform.localPosition = new Vector3(0f, 0f, 0f);
        SetBulbLights(_currentMap);
        _isAnimating = false;
    }

    private void Calculate()
    {
        Hex startingHex;
        switch (_mostCommonColor)
        {
            case BulbColor.Red:
                startingHex = new Hex(0, -2);
                break;
            case BulbColor.Yellow:
                startingHex = new Hex(2, -2);
                break;
            case BulbColor.Green:
                startingHex = new Hex(2, 0);
                break;
            case BulbColor.Blue:
                startingHex = new Hex(0, 2);
                break;
            case BulbColor.Purple:
                startingHex = new Hex(-2, 2);
                break;
            case BulbColor.White:
                startingHex = new Hex(-2, 0);
                break;
            default:
                throw new InvalidOperationException();
        }

    }

    private int GetProperty(BulbInfo[][] bulbinfo, int index)
    {
        switch (index)
        {
            case 0:
                return bulbinfo.Select(map => map.Count(b => b.IsTransparent && b.Color == BulbColor.Green)).Sum();
            case 1:
                return bulbinfo.Select(map => map.Count(b => b.IsLit && b.Color == BulbColor.White)).Sum();
            case 2:
                return Enumerable.Range(0, 36).Count(b => bulbinfo[0][b].IsTransparent == bulbinfo[1][b].IsTransparent);
            case 3:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 36)
                        .Where(i =>
                            i % 6 != 0 && i % 6 != 5 &&
                            i / 6 != 0 && i / 6 != 5 &&
                            map[i - 6].IsTransparent &&
                            map[i + 6].IsTransparent &&
                            map[i - 1].IsTransparent &&
                            map[i + 1].IsTransparent)
                        .Count());
            case 4:
                return bulbinfo.Select(map => map.Count(b => !b.IsLit && b.Color == BulbColor.White)).Sum();
            case 5:
                return bulbinfo.Select(map => map.Count(b => !b.IsLit && b.Color == BulbColor.Yellow)).Sum();
            case 6:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 36)
                        .Where(i =>
                            i % 6 != 5 && i / 6 != 5 &&
                            map[i].Color == map[i + 1].Color &&
                            map[i].Color == map[i + 6].Color &&
                            map[i].Color == map[i + 7].Color)
                        .Count());
            case 7:
                return bulbinfo.Select(map => map.Count(b => !b.IsTransparent && b.Color == BulbColor.Blue)).Sum();
            case 8:
                return bulbinfo.Select(map => map.Count(b => !b.IsTransparent && b.Color == BulbColor.Red)).Sum();
            case 9:
                return bulbinfo.Select(map => map.Count(b => !b.IsTransparent && b.Color == BulbColor.Green)).Sum();
            case 10:
                return bulbinfo.Select(map => map.Count(b => b.IsTransparent && b.Color == BulbColor.White)).Sum();
            case 11:
                return bulbinfo.Select(map => map.Count(b => b.IsTransparent && b.Color == BulbColor.Red)).Sum();
            case 12:
                return bulbinfo.Select(map => map.Count(b => b.IsTransparent && b.Color == BulbColor.Blue)).Sum();
            case 13:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 0 && b % 6 != 5 &&
                            b / 6 != 0 && b / 6 != 5 &&
                            !map[b - 6].IsTransparent &&
                            !map[b + 6].IsTransparent &&
                            !map[b - 1].IsTransparent &&
                            !map[b + 1].IsTransparent)
                        .Count());
            case 14:
                return bulbinfo.Select(map => map.Count(b => b.IsTransparent && b.Color == BulbColor.Yellow)).Sum();
            case 15:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 0 && b % 6 != 5 &&
                            b / 6 != 0 && b / 6 != 5 &&
                            !map[b - 6].IsLit &&
                            !map[b + 6].IsLit &&
                            !map[b - 1].IsLit &&
                            !map[b + 1].IsLit)
                        .Count());
            case 16:
                return bulbinfo.Select(map => map.Count(b => b.IsLit && b.Color == BulbColor.Red)).Sum();
            case 17:
                return bulbinfo.Select(map => map.Count(b => !b.IsTransparent && b.Color == BulbColor.White)).Sum();
            case 18:
                return Enumerable.Range(0, 36)
                    .Count(b => bulbinfo[0][b].Color == bulbinfo[1][b].Color);
            case 19:
                return bulbinfo.Select(map => map.Count(b => b.IsLit && b.Color == BulbColor.Red)).Sum();
            case 20:
                return bulbinfo.Select(map => map.Count(b => !b.IsTransparent && b.Color == BulbColor.Purple)).Sum();
            case 21:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 6).Count(r => Enumerable.Range(0, 6).Count(c => map[r * 6 + c].IsTransparent) == 3) +
                    Enumerable.Range(0, 6).Count(c => Enumerable.Range(0, 6).Count(r => map[r * 6 + c].IsTransparent) == 3)
                );
            case 22:
                return bulbinfo.Select(map => map.Count(b => !b.IsLit && b.Color == BulbColor.Purple)).Sum();
            case 23:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 5 && b / 6 != 5 &&
                            map[b].IsLit == map[b + 1].IsLit &&
                            map[b].IsLit == map[b + 6].IsLit &&
                            map[b].IsLit == map[b + 7].IsLit)
                        .Count());
            case 24:
                return bulbinfo.Select(map => map.Count(b => b.IsLit && b.Color == BulbColor.Green)).Sum();
            case 25:
                return bulbinfo.Select(map => map.Count(b => b.IsLit && b.Color == BulbColor.Blue)).Sum();
            case 26:
                return bulbinfo.Select(map => map.Count(b => b.IsLit && b.Color == BulbColor.Yellow)).Sum();
            case 27:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 6).Count(r => Enumerable.Range(0, 6).Count(c => map[r * 6 + c].IsLit) == 3) +
                    Enumerable.Range(0, 6).Count(c => Enumerable.Range(0, 6).Count(r => map[r * 6 + c].IsLit) == 3)
                );
            case 28:
                return bulbinfo.Select(map => map.Count(b => !b.IsTransparent && b.Color == BulbColor.Yellow)).Sum();
            case 29:
                return bulbinfo.Select(map => map.Count(b => !b.IsLit && b.Color == BulbColor.Blue)).Sum();
            case 30:
                return Enumerable.Range(0, 36)
                    .Count(b => bulbinfo[0][b].IsLit == bulbinfo[1][b].IsLit);
            case 31:
                return bulbinfo.Select(map => map.Count(b => !b.IsLit && b.Color == BulbColor.Green)).Sum();
            case 32:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 36).Count(b =>
                        (b >= 6 && map[b].Color == map[b - 6].Color) ||
                        (b < 30 && map[b].Color == map[b + 6].Color) ||
                        (b % 6 != 0 && map[b].Color == map[b - 1].Color) ||
                        (b % 6 != 5 && map[b].Color == map[b + 1].Color)
                    ));
            case 33:
                return bulbinfo.Select(map => map.Count(b => b.IsLit && b.Color == BulbColor.Purple)).Sum();
            case 34:
                return bulbinfo.Select(map => map.Count(b => b.IsTransparent && b.Color == BulbColor.Purple)).Sum();
            case 35:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 0 && b % 6 != 5 &&
                            b / 6 != 0 && b / 6 != 5 &&
                            map[b - 6].IsLit &&
                            map[b + 6].IsLit &&
                            map[b - 1].IsLit &&
                            map[b + 1].IsLit)
                        .Count());
            case 36:
                return bulbinfo.Sum(map =>
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 5 && b / 6 != 5 &&
                            map[b].IsTransparent == map[b + 1].IsTransparent &&
                            map[b].IsTransparent == map[b + 6].IsTransparent &&
                            map[b].IsTransparent == map[b + 7].IsTransparent)
                        .Count());
        }
        throw new InvalidOperationException();
    }
}
