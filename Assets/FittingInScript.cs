using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class FittingInScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    public KMSelectable[] SquareSels;
    public GameObject[] SquareObjs;
    public Material[] SquareMats;
    public KMSelectable FlipSel;
    public GameObject FlipParent;
    public GameObject[] ShapeObjs;
    public GameObject ShapeParent;
    public Material[] ShapeMats;
    public GameObject StatusLightObj;

    private sealed class Piece : IEquatable<Piece>
    {
        public int[] cells;
        public int rows;
        public int cols;

        public Piece(int[] cells, int rows, int cols)
        {
            this.cells = cells;
            this.rows = rows;
            this.cols = cols;
        }

        public bool Equals(Piece other)
        {
            return other != null && other.rows == rows && other.cols == cols && other.cells.SequenceEqual(cells);
        }

        public override int GetHashCode()
        {
            var hash = 47;
            for (int i = 0; i < cells.Length; i++)
                hash = unchecked(73 * hash + cells[i]);
            hash = unchecked(37 * hash + rows);
            hash = unchecked(61 * hash + cols);
            return hash;
        }
    }

    private Piece _piece;
    private List<Piece> _pieceTransformations = new List<Piece>();
    private bool[] _grid = new bool[100];
    private string _mainPieceStr = "";
    private string _gridStr;

    private int[] _piecePosIxs = new int[6];
    private bool[] _satisfied = new bool[6];

    private bool _gridFlippedUp;
    private bool _isAnimating;
    private bool _isSolving;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        GeneratePiece();
        CreateGrid();

        FlipSel.OnInteract += FlipPress;
        for (int i = 0; i < SquareSels.Length; i++)
            SquareSels[i].OnInteract += SquarePress(i);

        StatusLightObj.SetActive(false);
    }

    private bool FlipPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        FlipSel.AddInteractionPunch(0.5f);
        if (_moduleSolved || _isAnimating | _isSolving)
            return false;
        StartCoroutine(FlipBoard());
        return false;
    }

    private KMSelectable.OnInteractHandler SquarePress(int btn)
    {
        return delegate ()
        {
            if (_moduleSolved || !_gridFlippedUp || _isAnimating || _isSolving)
                return false;
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, SquareSels[btn].transform);
            SquareSels[btn].AddInteractionPunch(0.25f);
            if (_piecePosIxs.Contains(btn))
            {
                SquareObjs[btn].GetComponent<MeshRenderer>().material = SquareMats[2];
                _satisfied[Array.IndexOf(_piecePosIxs, btn)] = true;
                Debug.LogFormat("[Fitting In #{0}] Correctly pressed square {1}.", _moduleId, GetCoord(btn));
                if (!_satisfied.Contains(false))
                {
                    Debug.LogFormat("[Fitting In #{0}] Pressed all correct squares. Module solved.", _moduleId);
                    _isSolving = true;
                    StartCoroutine(FlipBoard());
                    StatusLightObj.transform.localPosition = new Vector3(0f, -0.01f, 0f);
                    StatusLightObj.SetActive(true);
                    for (int w = 0; w < ShapeObjs.Length; w++)
                        ShapeObjs[w].SetActive(false);
                }
            }
            else
            {
                Module.HandleStrike();
                SquareObjs[btn].GetComponent<MeshRenderer>().material = SquareMats[_grid[btn] ? 4 : 3];
                Debug.LogFormat("[Fitting In #{0}] Incorrectly pressed square {1}. Strike.", _moduleId, GetCoord(btn));
            }
            return false;
        };
    }

    private IEnumerator FlipBoard()
    {
        _isAnimating = true;
        var duration = 1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            FlipParent.transform.localEulerAngles = new Vector3(Easing.InOutQuad(elapsed, _gridFlippedUp ? 0f : 180f, _gridFlippedUp ? 180f : 0f, duration), 0f, 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        FlipParent.transform.localEulerAngles = new Vector3(_gridFlippedUp ? 180f : 0f, 0f, 0f);
        _gridFlippedUp = !_gridFlippedUp;
        if (_isSolving)
        {
            _moduleSolved = true;
            Module.HandlePass();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
        }
        _isAnimating = false;
    }

    private void GeneratePiece()
    {
        newPiece:
        var nums = Enumerable.Range(0, 36).ToArray().Shuffle().Take(6).ToArray();
        bool pieceCheck;
        List<List<int>> groups = new List<List<int>>();
        var grid = new int[36];
        for (int i = 0; i < 36; i++)
        {
            if (nums.Contains(i))
            {
                var tempGroups = GetAdjacents(i)
                    .Where(adj => grid[i] == grid[adj])
                    .Select(adj => groups.FirstOrDefault(clump => clump.Contains(adj)))
                    .Where(clump => clump != null)
                    .ToArray();
                var newGroup = new List<int>();
                foreach (var clump in tempGroups)
                {
                    groups.Remove(clump);
                    newGroup.AddRange(clump);
                }
                newGroup.Add(i);
                groups.Add(newGroup);
            }
        }
        if (groups.Count == 1)
            pieceCheck = true;
        else
            pieceCheck = false;
        if (!pieceCheck)
            goto newPiece;
        var initrows = nums.Select(i => i / 6).ToArray();
        var initcols = nums.Select(i => i % 6).ToArray();
        var rowList = new List<int>();
        var colList = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            if (initrows.Contains(i))
                rowList.Add(i);
            if (initcols.Contains(i))
                colList.Add(i);
        }
        var list = new List<int>();
        for (int r = 0; r < 6; r++)
            for (int c = 0; c < 6; c++)
                if (nums.Contains(r * 6 + c))
                    list.Add(r * 6 + c - rowList[0] * colList.Count() - colList[0] - (r * (6 - colList.Count)));
        if (rowList.Count >= 5 || colList.Count >= 5)
            goto newPiece;
        if (rowList.Count * colList.Count == 6)
            goto newPiece;
        _piece = new Piece(list.ToArray(), rowList.Count, colList.Count);
        _mainPieceStr = GetStringFromPiece(_piece);
        Debug.LogFormat("[Fitting In #{0}] Piece:", _moduleId);
        var tmpStr = _mainPieceStr.Split('\n').ToArray();
        for (int i = 0; i < tmpStr.Length; i++)
            Debug.LogFormat("[Fitting In #{0}] {1}", _moduleId, tmpStr[i]);
        _pieceTransformations = GetPieceTransformations().ToList();
        var pieceObjArr = new int[6];
        for (int i = 0; i < 6; i++)
            pieceObjArr[i] = convertGrids(_piece.cells[i], _piece.cols, 4);
        var rndColor = Rnd.Range(0, ShapeMats.Length);
        for (int i = 0; i < 16; i++)
        {
            ShapeObjs[i].GetComponent<MeshRenderer>().material = ShapeMats[rndColor];
            ShapeObjs[i].SetActive(pieceObjArr.Contains(i));
        }
        ShapeParent.transform.localPosition = new Vector3(ShapeParent.transform.localPosition.x + 0.015f * (4 - _piece.cols), ShapeParent.transform.localPosition.y, ShapeParent.transform.localPosition.z + 0.015f * (4 - _piece.rows));
    }

    private void CreateGrid()
    {
        StartFromScratch:
        _grid = new bool[100];
        NextCell:
        var rndCell = Rnd.Range(0, _grid.Length);
        if (_grid[rndCell])
            goto NextCell;
        _grid[rndCell] = true;
        int count = CheckGridState();
        _gridStr = "";
        var tempStr = "";
        for (int i = 0; i < 100; i++)
        {
            _gridStr += _grid[i] ? "#" : "*";
            tempStr += _grid[i] ? "#" : "*";
            if (i % 10 == 9 && i != 99)
                _gridStr += "\n";
        }
        if (count == 0)
            goto NextCell;
        if (count == 1)
        {
            int c = 0;
            for (int i = 0; i < _gridStr.Length; i++)
                if (_gridStr[i] == '#')
                    c++;
            if (c < 50)
                goto StartFromScratch;
            else
                goto Finished;
        }
        if (count > 1)
            goto StartFromScratch;
        Finished:;
        Debug.LogFormat("[Fitting In #{0}] Grid: (whites are #, blacks are *)", _moduleId);
        var tmpStr = _gridStr.Split('\n');
        for (int i = 0; i < tmpStr.Length; i++)
            Debug.LogFormat("[Fitting In #{0}] {1}", _moduleId, tmpStr[i]);
        for (int i = 0; i < SquareObjs.Length; i++)
            SquareObjs[i].GetComponent<MeshRenderer>().material = SquareMats[tempStr[i] == '#' ? 0 : 1];
        Debug.LogFormat("[Fitting In #{0}] Correct cells: {1}", _moduleId, _piecePosIxs.Select(i => GetCoord(i)).Join(", "));
    }

    private string GetCoord(int num)
    {
        return "ABCDEFGHIJ".Substring(num % 10, 1) + (num / 10 != 9 ? "1234567890".Substring(num / 10, 1) : "10");
    }

    private int CheckGridState()
    {
        int count = 0;
        var ppi = new List<int>();
        for (int i = 0; i < _pieceTransformations.Count; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                if (j % 10 >= (11 - _pieceTransformations[i].cols) || j / 10 >= 11 - _pieceTransformations[i].rows)
                    continue;
                bool valid = true;
                for (int p = 0; p < _pieceTransformations[i].cells.Length; p++)
                {
                    if (!_grid[convertGrids(_pieceTransformations[i].cells[p], _pieceTransformations[i].cols, 10) + j])
                        valid = false;
                }
                if (valid)
                {
                    ppi = new List<int>();
                    for (int w = 0; w < 6; w++)
                        ppi.Add(convertGrids(_pieceTransformations[i].cells[w], _pieceTransformations[i].cols, 10) + j);
                    count++;
                }
            }
        }
        _piecePosIxs = ppi.ToArray();
        return count;
    }

    int convertGrids(int ix, int prevWidth, int newWidth)
    {
        return (ix % prevWidth) + newWidth * (ix / prevWidth);
    }

    private IEnumerable<Piece> GetPieceTransformations()
    {
        var pieceList = new HashSet<Piece>();
        List<string> list;

        list = new List<string>();
        for (int rows = 0; rows < _piece.rows; rows++)
        {
            string str = "";
            for (int cols = 0; cols < _piece.cols; cols++)
                str += _mainPieceStr.Substring(rows * _piece.cols + cols + rows, 1);
            list.Add(str);
        }
        pieceList.Add(GetPieceFromList(list));

        list = new List<string>();
        for (int rows = 0; rows < _piece.rows; rows++)
        {
            string str = "";
            for (int cols = _piece.cols - 1; cols >= 0; cols--)
                str += _mainPieceStr.Substring(rows * _piece.cols + cols + rows, 1);
            list.Add(str);
        }
        pieceList.Add(GetPieceFromList(list));

        list = new List<string>();
        for (int rows = _piece.rows - 1; rows >= 0; rows--)
        {
            string str = "";
            for (int cols = 0; cols < _piece.cols; cols++)
                str += _mainPieceStr.Substring(rows * _piece.cols + cols + rows, 1);
            list.Add(str);
        }
        pieceList.Add(GetPieceFromList(list));

        list = new List<string>();
        for (int rows = _piece.rows - 1; rows >= 0; rows--)
        {
            string str = "";
            for (int cols = _piece.cols - 1; cols >= 0; cols--)
                str += _mainPieceStr.Substring(rows * _piece.cols + cols + rows, 1);
            list.Add(str);
        }
        pieceList.Add(GetPieceFromList(list));

        list = new List<string>();
        for (int rows = 0; rows < _piece.cols; rows++)
        {
            string str = "";
            for (int cols = 0; cols < _piece.rows; cols++)
                str += _mainPieceStr.Substring(cols * _piece.cols + rows + cols, 1);
            list.Add(str);
        }
        pieceList.Add(GetPieceFromList(list));

        list = new List<string>();
        for (int rows = 0; rows < _piece.cols; rows++)
        {
            string str = "";
            for (int cols = _piece.rows - 1; cols >= 0; cols--)
                str += _mainPieceStr.Substring(cols * _piece.cols + rows + cols, 1);
            list.Add(str);
        }
        pieceList.Add(GetPieceFromList(list));

        list = new List<string>();
        for (int rows = _piece.cols - 1; rows >= 0; rows--)
        {
            string str = "";
            for (int cols = 0; cols < _piece.rows; cols++)
                str += _mainPieceStr.Substring(cols * _piece.cols + rows + cols, 1);
            list.Add(str);
        }
        pieceList.Add(GetPieceFromList(list));

        list = new List<string>();
        for (int rows = _piece.cols - 1; rows >= 0; rows--)
        {
            string str = "";
            for (int cols = _piece.rows - 1; cols >= 0; cols--)
                str += _mainPieceStr.Substring(cols * _piece.cols + rows + cols, 1);
            list.Add(str);
        }
        pieceList.Add(GetPieceFromList(list));

        return pieceList;
    }

    private Piece GetPieceFromList(List<string> stringList)
    {
        var list = new List<int>();
        for (int i = 0; i < stringList.Count; i++)
        {
            for (int j = 0; j < stringList[i].Length; j++)
                if (stringList[i][j] == '#')
                    list.Add(i * stringList[i].Length + j);
        }
        return new Piece(list.ToArray(), stringList.Count, stringList[0].Length);
    }

    private string GetStringFromPiece(Piece piece)
    {
        string str = "";
        for (int i = 0; i < piece.cols * piece.rows; i++)
        {
            if (piece.cells.Contains(i))
                str += "#";
            else
                str += "*";
            if (i % piece.cols == piece.cols - 1 && i != piece.cols * piece.rows - 1)
                str += "\n";
        }
        return str;
    }

    private IEnumerable<int> GetAdjacents(int num)
    {
        var list = new List<int>();
        if (num % 6 != 0)
            list.Add(num - 1);
        if (num % 6 != 5)
            list.Add(num + 1);
        if (num / 6 != 0)
            list.Add(num - 6);
        if (num / 6 != 5)
            list.Add(num + 6);
        return list;
    }

#pragma warning disable 0414
    private string TwitchHelpMessage = "!{0} flip [Flips the board over.] | !{0} press A1 [Presses button A1.] | Columns are labelled A-J. Rows are labelled 1-10.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        m = Regex.Match(command, @"^\s*flip\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            FlipSel.OnInteract();
            yield break;
        }
        var parameters = command.ToUpperInvariant().Split(' ');
        m = Regex.Match(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            yield break;
        if (!_gridFlippedUp)
        {
            yield return "sendtochaterror The grid isn't flipped up! Use the 'flip' command.";
            yield break;
        }
        var list = new List<int>();
        for (int i = 1; i < parameters.Length; i++)
        {
            if (parameters[i].Length != 2 && parameters[i].Length != 3)
            {
                yield return "sendtochaterror " + parameters[i] + " is not a valid button! Press a button with a letter-number coordinate.";
                yield break;
            }
            if (!((parameters[i][0] >= 'A') && (parameters[i][0] <= 'Z')))
            {
                yield return "sendtochaterror " + parameters[i] + " is not a valid button! Press a button with a letter-number coordinate.";
                yield break;
            }
            int val;
            if (!int.TryParse(parameters[i].Substring(1), out val) || val < 1 || val > 10)
            {
                yield return "sendtochaterror " + parameters[i] + " is not a valid button! Press a button with a letter-number coordinate.";
                yield break;
            }
            list.Add(parameters[i][0] - 'A' + (val - 1) * 10);
        }
        yield return null;
        for (int i = 0; i < list.Count; i++)
        {
            SquareSels[list[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (!_gridFlippedUp)
            FlipSel.OnInteract();
        while (_isAnimating)
            yield return true;
        for (int i = 0; i < _piecePosIxs.Length; i++)
        {
            if (!_satisfied[i])
            {
                SquareSels[_piecePosIxs[i]].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
        while (!_moduleSolved)
            yield return true;
    }
}
