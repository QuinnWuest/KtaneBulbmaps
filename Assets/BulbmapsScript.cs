using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using Bulbmaps;
using System.Collections.Generic;

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

    public GameObject SwitchCasing;
    public KMSelectable[] ButtonSels;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private BulbColor _mostCommonColor;

    private int _solution;

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
        new Color32(60, 80, 255, 255),    // blue
        new Color32(180, 80, 255, 255),     // purple
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

    public BulbInfo[] _bulbMapInfo = new BulbInfo[36];

    private static readonly string[] _dirNames = new string[]
    {
        "northwest",
        "north",
        "northeast",
        "southeast",
        "south",
        "southwest"
    };

    private static readonly string[] _ruleStrings = new string[]
    {
        "Transparent green bulbs",
        "Lit white bulbs",
        "Quadrants with a majority of lit bulbs",
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
        "Quadrants with a majority of transparent bulbs",
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
        "Quadrants with a majority of red, white, and blue bulbs",
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
        for (int i = 0; i < ButtonSels.Length; i++)
            ButtonSels[i].OnInteract += ButtonPress(i);
        foreach (var l in BulbLights)
            l.range *= transform.lossyScale.x;

        Generate();

        SetBulbs();
        SetBulbLights();
    }

    private KMSelectable.OnInteractHandler ButtonPress(int btn)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ButtonSels[btn].transform);
            ButtonSels[btn].AddInteractionPunch(0.5f);
            if (_moduleSolved)
                return false;
            if (btn == _solution)
            {
                Debug.LogFormat("[Bulbmaps #{0}] Correctly pressed {1}. Module solved.", _moduleId, btn + 1);
                _moduleSolved = true;
                Module.HandlePass();
            }
            else
            {
                Debug.LogFormat("[Bulbmaps #{0}] Incorrectly pressed {1}. Strike.", _moduleId, btn + 1);
                Module.HandleStrike();
            }
            return false;
        };
    }

    private void LogBulbmaps()
    {

        Debug.LogFormat("[Bulbmaps #{0}] Bulbmap:", _moduleId);
        for (int row = 0; row < 6; row++)
        {
            var colors = Enumerable.Range(0, 6).Select(col => "RYGBPW"[(int)_bulbMapInfo[col + (6 * row)].Color]).Join("");
            var transparents = Enumerable.Range(0, 6).Select(col => _bulbMapInfo[col + (6 * row)].IsTransparent ? "t" : "o").Join("");
            var lightStates = Enumerable.Range(0, 6).Select(col => _bulbMapInfo[col + (6 * row)].IsLit ? "1" : "0").Join("");
            Debug.LogFormat("[Bulbmaps #{0}] {1}", _moduleId, Enumerable.Range(0, 6).Select(i => colors[i].ToString() + transparents[i].ToString() + lightStates[i].ToString()).Join(" "));
        }
    }

    private void SetBulbs()
    {
        for (int i = 0; i < 36; i++)
        {
            BulbObjs[i].GetComponent<MeshRenderer>().material =
                BulbMats[(int)_bulbMapInfo[i].Color + (_bulbMapInfo[i].IsTransparent ? 6 : 0)];
            BulbLights[i].color = _lightColors[(int)_bulbMapInfo[i].Color];
        }
    }

    private void SetBulbLights()
    {
        for (int i = 0; i < 36; i++)
            BulbLights[i].enabled = _bulbMapInfo[i].IsLit;
    }

    private void Generate()
    {
        tryAgain:
        var logList = new List<string>();
        for (int i = 0; i < 36; i++)
        {
            var c = (BulbColor)Rnd.Range(0, 6);
            var l = Rnd.Range(0, 2) == 0;
            var t = Rnd.Range(0, 2) == 0;
            _bulbMapInfo[i] = new BulbInfo(c, l, t);
        }

        var colorCounts = Enum.GetValues(typeof(BulbColor))
            .Cast<BulbColor>()
            .ToDictionary(c => c, c => 0);

        for (int i = 0; i < 36; i++)
            colorCounts[_bulbMapInfo[i].Color]++;

        int maxCount = colorCounts.Values.Max();

        var tiedColors = colorCounts
            .Where(kv => kv.Value == maxCount)
            .Select(kv => kv.Key)
            .ToList();

        if (tiedColors.Count > 1)
        {
            BulbColor winnerColor = tiedColors[Rnd.Range(0, tiedColors.Count)];
            _mostCommonColor = winnerColor;
            for (int i = 0; i < 36; i++)
            {
                if (_bulbMapInfo[i].Color != winnerColor)
                {
                    _bulbMapInfo[i].Color = winnerColor;
                    goto Done;
                }
            }
        }
        else
            _mostCommonColor = tiedColors[0];
        Done:;

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

        logList.Add(string.Format("[Bulbmaps #{0}] The most common colored bulb is {1}.", _moduleId, _mostCommonColor, startingHex));

        int count = 0;
        Hex currentHex = startingHex;
        bool[] taken = new bool[37];
        taken[GetIndex(currentHex)] = true;
        int val;
        while (true)
        {
            count++;
            if (count > 15)
                goto tryAgain;

            val = GetProperty(_bulbMapInfo, currentHex);
            int dir = val % 6;

            var newHex = currentHex;

            do
                newHex = Move(newHex, dir);
            while
                (newHex != currentHex && taken[GetIndex(newHex)]);

            logList.Add(string.Format("[Bulbmaps #{0}] Position: {1}", _moduleId, currentHex));
            logList.Add(string.Format("[Bulbmaps #{0}] Rule: {1}; Value: {2}. Move {3}.",
                _moduleId,
                _ruleStrings[GetIndex(currentHex)],
                val,
                _dirNames[dir],
                newHex
                ));

            if (newHex == currentHex)
                break;

            currentHex = newHex;
            taken[GetIndex(newHex)] = true;
        }

        _solution = ((val - 1) + 6) % 6;
        LogBulbmaps();
        foreach (var log in logList)
            Debug.Log(log);

        Debug.LogFormat("[Bulbmaps #{0}] The button to press is {1}.", _moduleId, _solution + 1);
    }

    private Hex Move(Hex fromHex, int dir)
    {
        var next = fromHex.GetNeighbor(dir);
        if (next.Distance <= 3)
            return next;

        int opposite = (dir + 3) % 6;

        var h = fromHex;
        var last = h;

        while (h.Distance <= 3)
        {
            last = h;
            h = h.GetNeighbor(opposite);
        }
        return last;
    }

    private int GetIndex(Hex hex)
    {
        if (hex.R == -3)
            return hex.Q;
        if (hex.R == -2)
            return hex.Q + 1 + 4;
        if (hex.R == -1)
            return hex.Q + 2 + 9;
        if (hex.R == 0)
            return hex.Q + 3 + 15;
        if (hex.R == 1)
            return hex.Q + 3 + 22;
        if (hex.R == 2)
            return hex.Q + 3 + 28;
        if (hex.R == 3)
            return hex.Q + 3 + 33;
        throw new InvalidOperationException();
    }

    private int GetProperty(BulbInfo[] bulbinfo, Hex hex)
    {
        switch (GetIndex(hex))
        {
            case 0:
                return bulbinfo.Count(b => b.IsTransparent && b.Color == BulbColor.Green);
            case 1:
                return bulbinfo.Count(b => b.IsLit && b.Color == BulbColor.White);
            case 2:
                return new[]
                {
                    Enumerable.Range(0, 3).SelectMany(r => Enumerable.Range(0, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(0, 3).SelectMany(r => Enumerable.Range(3, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(3, 3).SelectMany(r => Enumerable.Range(0, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(3, 3).SelectMany(r => Enumerable.Range(3, 3).Select(c => bulbinfo[r * 6 + c]))
                }
                .Count(q => q.Count(b => b.IsLit) >= 5);
            case 3:
                return
                    Enumerable.Range(0, 36)
                        .Where(i =>
                            i % 6 != 0 && i % 6 != 5 &&
                            i / 6 != 0 && i / 6 != 5 &&
                            bulbinfo[i - 6].IsTransparent &&
                            bulbinfo[i + 6].IsTransparent &&
                            bulbinfo[i - 1].IsTransparent &&
                            bulbinfo[i + 1].IsTransparent)
                        .Count();
            case 4:
                return bulbinfo.Count(b => !b.IsLit && b.Color == BulbColor.White);
            case 5:
                return bulbinfo.Count(b => !b.IsLit && b.Color == BulbColor.Yellow);
            case 6:
                return
                    Enumerable.Range(0, 36)
                        .Where(i =>
                            i % 6 != 5 && i / 6 != 5 &&
                            bulbinfo[i].Color == bulbinfo[i + 1].Color &&
                            bulbinfo[i].Color == bulbinfo[i + 6].Color &&
                            bulbinfo[i].Color == bulbinfo[i + 7].Color)
                        .Count();
            case 7:
                return bulbinfo.Count(b => !b.IsTransparent && b.Color == BulbColor.Blue);
            case 8:
                return bulbinfo.Count(b => !b.IsTransparent && b.Color == BulbColor.Red);
            case 9:
                return bulbinfo.Count(b => !b.IsTransparent && b.Color == BulbColor.Green);
            case 10:
                return bulbinfo.Count(b => b.IsTransparent && b.Color == BulbColor.White);
            case 11:
                return bulbinfo.Count(b => b.IsTransparent && b.Color == BulbColor.Red);
            case 12:
                return bulbinfo.Count(b => b.IsTransparent && b.Color == BulbColor.Blue);
            case 13:
                return
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 0 && b % 6 != 5 &&
                            b / 6 != 0 && b / 6 != 5 &&
                            !bulbinfo[b - 6].IsTransparent &&
                            !bulbinfo[b + 6].IsTransparent &&
                            !bulbinfo[b - 1].IsTransparent &&
                            !bulbinfo[b + 1].IsTransparent)
                        .Count();
            case 14:
                return bulbinfo.Count(b => b.IsTransparent && b.Color == BulbColor.Yellow);
            case 15:
                return
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 0 && b % 6 != 5 &&
                            b / 6 != 0 && b / 6 != 5 &&
                            !bulbinfo[b - 6].IsLit &&
                            !bulbinfo[b + 6].IsLit &&
                            !bulbinfo[b - 1].IsLit &&
                            !bulbinfo[b + 1].IsLit)
                        .Count();
            case 16:
                return bulbinfo.Count(b => b.IsLit && b.Color == BulbColor.Red);
            case 17:
                return bulbinfo.Count(b => !b.IsTransparent && b.Color == BulbColor.White);
            case 18:
                return new[]
                {
                    Enumerable.Range(0, 3).SelectMany(r => Enumerable.Range(0, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(0, 3).SelectMany(r => Enumerable.Range(3, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(3, 3).SelectMany(r => Enumerable.Range(0, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(3, 3).SelectMany(r => Enumerable.Range(3, 3).Select(c => bulbinfo[r * 6 + c]))
                }
                .Count(q => q.Count(b => b.IsTransparent) >= 5);
            case 19:
                return bulbinfo.Count(b => b.IsLit && b.Color == BulbColor.Red);
            case 20:
                return bulbinfo.Count(b => !b.IsTransparent && b.Color == BulbColor.Purple);
            case 21:
                return
                    Enumerable.Range(0, 6).Count(r => Enumerable.Range(0, 6).Count(c => bulbinfo[r * 6 + c].IsTransparent) == 3) +
                    Enumerable.Range(0, 6).Count(c => Enumerable.Range(0, 6).Count(r => bulbinfo[r * 6 + c].IsTransparent) == 3);
            case 22:
                return bulbinfo.Count(b => !b.IsLit && b.Color == BulbColor.Purple);
            case 23:
                return
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 5 && b / 6 != 5 &&
                            bulbinfo[b].IsLit == bulbinfo[b + 1].IsLit &&
                            bulbinfo[b].IsLit == bulbinfo[b + 6].IsLit &&
                            bulbinfo[b].IsLit == bulbinfo[b + 7].IsLit)
                        .Count();
            case 24:
                return bulbinfo.Count(b => b.IsLit && b.Color == BulbColor.Green);
            case 25:
                return bulbinfo.Count(b => b.IsLit && b.Color == BulbColor.Blue);
            case 26:
                return bulbinfo.Count(b => b.IsLit && b.Color == BulbColor.Yellow);
            case 27:
                return
                    Enumerable.Range(0, 6).Count(r => Enumerable.Range(0, 6).Count(c => bulbinfo[r * 6 + c].IsLit) == 3) +
                    Enumerable.Range(0, 6).Count(c => Enumerable.Range(0, 6).Count(r => bulbinfo[r * 6 + c].IsLit) == 3);
            case 28:
                return bulbinfo.Count(b => !b.IsTransparent && b.Color == BulbColor.Yellow);
            case 29:
                return bulbinfo.Count(b => !b.IsLit && b.Color == BulbColor.Blue);
            case 30:
                return new[]
                {
                    Enumerable.Range(0, 3).SelectMany(r => Enumerable.Range(0, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(0, 3).SelectMany(r => Enumerable.Range(3, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(3, 3).SelectMany(r => Enumerable.Range(0, 3).Select(c => bulbinfo[r * 6 + c])),
                    Enumerable.Range(3, 3).SelectMany(r => Enumerable.Range(3, 3).Select(c => bulbinfo[r * 6 + c]))
                }
                .Count(q => q.Count(b =>
                    b.Color == BulbColor.Red ||
                    b.Color == BulbColor.White ||
                    b.Color == BulbColor.Blue) >= 5);
            case 31:
                return bulbinfo.Count(b => !b.IsLit && b.Color == BulbColor.Green);
            case 32:
                return
                    Enumerable.Range(0, 36).Count(b =>
                        (b >= 6 && bulbinfo[b].Color == bulbinfo[b - 6].Color) ||
                        (b < 30 && bulbinfo[b].Color == bulbinfo[b + 6].Color) ||
                        (b % 6 != 0 && bulbinfo[b].Color == bulbinfo[b - 1].Color) ||
                        (b % 6 != 5 && bulbinfo[b].Color == bulbinfo[b + 1].Color)
                    );
            case 33:
                return bulbinfo.Count(b => b.IsLit && b.Color == BulbColor.Purple);
            case 34:
                return bulbinfo.Count(b => b.IsTransparent && b.Color == BulbColor.Purple);
            case 35:
                return
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 0 && b % 6 != 5 &&
                            b / 6 != 0 && b / 6 != 5 &&
                            bulbinfo[b - 6].IsLit &&
                            bulbinfo[b + 6].IsLit &&
                            bulbinfo[b - 1].IsLit &&
                            bulbinfo[b + 1].IsLit)
                        .Count();
            case 36:
                return
                    Enumerable.Range(0, 36)
                        .Where(b =>
                            b % 6 != 5 && b / 6 != 5 &&
                            bulbinfo[b].IsTransparent == bulbinfo[b + 1].IsTransparent &&
                            bulbinfo[b].IsTransparent == bulbinfo[b + 6].IsTransparent &&
                            bulbinfo[b].IsTransparent == bulbinfo[b + 7].IsTransparent)
                        .Count();
        }
        throw new InvalidOperationException();
    }
}
