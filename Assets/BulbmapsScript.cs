using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

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

    private int[][] _bulbColors = new int[2][] { new int[36], new int[36] };
    private bool[][] _bulbLightStates = new bool[4][] { new bool[36], new bool[36], new bool[36], new bool[36] };
    private int[] _bulbStateOrder;
    private int _currentBulbState;
    private bool[][] _bulbTransparencies = new bool[2][] { new bool[36], new bool[36] };
    private int _currentBulbMap;
    private bool _isAnimating;

    private static readonly Color32[] _lightColors = new Color32[] { new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255), new Color32(80, 100, 255, 255), new Color32(255, 0, 255, 255), new Color32(255, 255, 0, 255), new Color32(255, 255, 255, 255) };
    private static readonly int[][] _quadrants = new int[4][] {
        new int[] { 0, 1, 2, 6, 7, 8, 12, 13, 14 },
        new int[] { 3, 4, 5, 9, 10, 11, 15, 16, 17 },
        new int[] { 18, 19, 20, 24, 25, 26, 30, 31, 32 },
        new int[] { 21, 22, 23, 27, 28, 29, 33, 34, 35 }
    };
    private static readonly int[] _quadrantOrder = new int[8] { 0, 1, 2, 3, 3, 2, 1, 0 };

    private bool[] _bitmap = new bool[64];
    private bool[][] _bitmapRows = new bool[8][] { new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8] };

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        SwitchSel.OnInteract += SwitchPress;
        for (int i = 0; i < ButtonSels.Length; i++)
            ButtonSels[i].OnInteract += ButtonPress(i);

        for (int j = 0; j < 2; j++)
        {
            for (int i = 0; i < 36; i++)
            {
                _bulbColors[j][i] = Rnd.Range(0, 6);
                _bulbTransparencies[j][i] = Rnd.Range(0, 2) == 0;
            }
        }
        for (int i = 0; i < _bulbLightStates.Length; i++)
            _bulbLightStates[i] = GenerateBulbLightState(i);
        _bulbStateOrder = Enumerable.Range(0, 4).ToArray().Shuffle();
        foreach (var l in BulbLights)
            l.range *= transform.lossyScale.x;
        SetBulbs();
        SetBulbLights();

        for (int map = 0; map < 2; map++)
        {
            Debug.LogFormat("[Bulbmaps #{0}] Bulbmap on the {1} page:", _moduleId, map == 0 ? "O" : "I");
            for (int row = 0; row < 6; row++)
            {
                var colors = Enumerable.Range(0, 6).Select(i => "RGBMYW"[_bulbColors[map][i + (6 * row)]]).Join("");
                var transparents = Enumerable.Range(0, 6).Select(i => _bulbTransparencies[map][i + (6 * row)] ? "t" : "o").Join("");
                Debug.LogFormat("[Bulbmaps #{0}] {1}", _moduleId, Enumerable.Range(0, 6).Select(i => colors[i].ToString() + transparents[i].ToString()).Join(" "));
            }
        }
    }

    private KMSelectable.OnInteractHandler ButtonPress(int btn)
    {
        return delegate ()
        {

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

    private bool[] GenerateBulbLightState(int stateNum)
    {
        var s = stateNum + 1;
        var map = new int[36];
        while (map.Count(i => i == 1) != s * 6)
        {
            
        }
        var arr = map.Select(i => i == 1).ToArray();
        Debug.Log(arr.Select(i => i ? "#" : ".").Join(""));
        return arr;
    }

    private void SetBulbs()
    {
        for (int i = 0; i < 36; i++)
        {
            BulbObjs[i].GetComponent<MeshRenderer>().material = BulbMats[_bulbColors[_currentBulbMap][i] + (_bulbTransparencies[_currentBulbMap][i] ? 6 : 0)];
            BulbLights[i].color = _lightColors[_bulbColors[_currentBulbMap][i]];
        }
    }

    private void SetBulbLights()
    {
        for (int i = 0; i < 36; i++)
            BulbLights[i].enabled = _bulbLightStates[_currentBulbMap][i];
    }

    private IEnumerator FlipSwitch()
    {
        var duration = 0.1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SwitchCasing.transform.localEulerAngles = new Vector3(180f, 0f, Easing.InOutQuad(elapsed, _currentBulbMap * 30f, (_currentBulbMap + 1) % 2 * 30f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        SwitchCasing.transform.localEulerAngles = new Vector3(180f, 0f, (_currentBulbMap + 1) % 2 * 30f);
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
        _currentBulbMap = (_currentBulbMap + 1) % 2;
        yield return null;
        SetBulbs();
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
        SetBulbLights();
        _isAnimating = false;
    }
}
