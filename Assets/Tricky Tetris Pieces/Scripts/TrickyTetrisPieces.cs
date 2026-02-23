using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class TrickyTetrisPieces : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable[] TileSelectables;
    public MeshRenderer[] TileRenderers;
    public GameObject[] TileObjects;

    public Material[] TileMaterials;

    public MeshRenderer[] BlankTiles;
    public TextMesh ColorText;
    public TextMesh TimerText;

    public MeshRenderer ModuleBacking;
    public Material[] ModuleBackingMaterials;

    public KMColorblindMode ColorblindMode;
    public KMSelectable Switch;
    public Transform SwitchPosition;

    public AudioSource MainMusic;

    // Reference info
    private readonly char[] MANUAL_GRID = {
        'Z', 'Z', 'L', 'L', 'J', 'J', 'I', 'I', 'I', 'I',
        'I', 'Z', 'Z', 'L', 'J', 'L', 'L', 'L', 'S', 'S',
        'I', 'O', 'O', 'L', 'J', 'L', 'T', 'S', 'S', 'I',
        'I', 'O', 'O', 'S', 'S', 'T', 'T', 'T', 'L', 'I',
        'I', 'J', 'S', 'S', 'O', 'O', 'L', 'L', 'L', 'I',
        'S', 'J', 'J', 'J', 'O', 'O', 'J', 'J', 'J', 'I',
        'S', 'S', 'T', 'T', 'T', 'L', 'O', 'O', 'J', 'T',
        'T', 'S', 'J', 'T', 'Z', 'L', 'O', 'O', 'T', 'T',
        'T', 'T', 'J', 'Z', 'Z', 'L', 'L', 'Z', 'Z', 'T',
        'T', 'J', 'J', 'Z', 'I', 'I', 'I', 'I', 'Z', 'Z'};

    private readonly int[] MANUAL_INDICES = { 23, 25, 27, 47, 67, 76, 74, 72, 52, 32 };

    private readonly char[] SHAPE_NAMES = { 'T', 'J', 'Z', 'O', 'S', 'L', 'I' };
    private readonly short[] SHAPE_ROT_AMOUNTS = { 4, 4, 2, 1, 2, 4, 2 };

    private readonly int DEFAULT_TIMER = 100;
    private readonly int ID_INCREASE_FACTOR = 64;

    // Testing info
    private readonly bool USE_TEST_GRID = true;
    private readonly float TEST_MUSIC_VOLUME = 0.5f;

    // Solving info
    private TetrisPalette[] palettes;
    private Dictionary<char, short> shapeColorKey = new Dictionary<char, short>();

    private TetrisPiece[] pieces = new TetrisPiece[50];
    private PieceNode[] nodes = new PieceNode[50];
    private int[] nodeOrder = new int[50];

    private int[] gridIDs = new int[200];
    private char[] gridShapes = new char[200];

    private short[] pieceCounts = new short[7];

    private int firstPieceIndex;
    private int firstPiecePalette;
    private char firstPieceShape;
    private int manualIndex;

    private int secondPalette;
    private char secondPieceShape;
    private bool noSecondPiece = false;
    private bool halfModulesSolved = false;

    private int moduleTimer = 0;
    private bool canCountDown = false;

    private bool canPress = false;
    private bool stageTwo = false;

    private bool switchFlipped = false;

    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved = false;

    // Mod settings
    private TrickyTetrisPiecesSettings Settings;
    sealed class TrickyTetrisPiecesSettings {
        public bool PlayMusic = true;
        public bool SwitchOnByDefault = false;
        public bool AlternateStrikeSound = false;
    }


    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

        // Module Settings
        var modConfig = new ModConfig<TrickyTetrisPiecesSettings>("TrickyTetrisPieces");
        Settings = modConfig.Settings;
        modConfig.Settings = Settings;

        for (int i = 0; i < TileSelectables.Length; i++) {
            int j = i;
            TileSelectables[i].OnInteract += delegate () { PressTile(j); return false; };
            TileSelectables[i].OnHighlight += delegate () { HighlightTile(j); };
            TileSelectables[i].OnHighlightEnded += delegate () { HighlightTile(-1); };
        }

        Switch.OnInteract += delegate () { PressSwitch(); return false; };

        // Stops music if bomb explodes
        Bomb.OnBombExploded += delegate () {
            if (MainMusic.isPlaying)
                MainMusic.Stop();
        };
	}

    // Ran as game returns to office
    private void OnDestroy() {
        if (MainMusic.isPlaying)
            MainMusic.Stop();
    }

    // Gets information
    private void Start() {
        // Adds all the palettes
        palettes = AllPalettes.AddTetrisPalettes();

        // Assigns the pieces to the proper palette subentry
        shapeColorKey.Add('T', 0);
        shapeColorKey.Add('J', 2);
        shapeColorKey.Add('Z', 1);
        shapeColorKey.Add('O', 0);
        shapeColorKey.Add('S', 2);
        shapeColorKey.Add('L', 1);
        shapeColorKey.Add('I', 0);

        // Turns off all the blank tiles
        for (int i = 0; i < BlankTiles.Length; i++)
            BlankTiles[i].enabled = false;

        // Turns off all the selectable tiles (for now)
        for (int i = 0; i < TileRenderers.Length; i++)
            TileObjects[i].SetActive(false);

        // Sets the volume for the music
        try {
            MainMusic.volume = GameMusicControl.GameSFXVolume;
        }

        catch (NullReferenceException) {
            MainMusic.volume = TEST_MUSIC_VOLUME;
        }

        // Checks if the switch should be pre-flipped
        if (Settings.SwitchOnByDefault || ColorblindMode.ColorblindModeActive) {
            switchFlipped = true;
            SwitchPosition.localPosition = new Vector3(0.25f, 1.5f, 0.0f);
        }

        ResetModule();
    }

    // Resets or initates the module
    private void ResetModule() {
        // Resets info
        stageTwo = false;
        TimerText.text = "WAIT...";

        for (int i = 0; i < gridIDs.Length; i++) {
            gridIDs[i] = 0;
            gridShapes[i] = '.';
        }

        for (int i = 0; i < pieceCounts.Length; i++)
            pieceCounts[i] = 0;

        // Generates the grid
        if (USE_TEST_GRID)
            GenerateTestGrid();

        else
            GenerateGrid();

        // Chooses the correct piece
        firstPieceIndex = UnityEngine.Random.Range(0, pieces.Length);
        firstPieceShape = pieces[firstPieceIndex].GetShape();

        ChoosePalettes(); // Chooses the palettes for each piece
        CreateNodes(); // Creates the nodes for each piece
        SortNodes(); // Sorts the nodes for each piece
        GetManualIndex(); // Finds the tile index in the manual from the first piece

        StartCoroutine(WipeModule(true, 1)); // Reveals the grid 
    }


    // Generates the grid
    private void GenerateGrid() {
        TetrisGridFiller.GenerateTetrisFill(); // Written by Timwi - repurposed from The Blue Button
        Debug.LogFormat("[Tricky Tetris Pieces #{0}] Grid generated successfully.", moduleId);
    }

    // Generates the testing grid
    private void GenerateTestGrid() {
        for (int i = 0; i < MANUAL_GRID.Length; i++)
            gridShapes[i] = MANUAL_GRID[i];

        for (int i = 100; i < gridShapes.Length; i++)
            gridShapes[i] = gridShapes[i - 100];

        int[] testGridIDs = {
            0, 0, 1, 1, 2, 2, 3, 3, 3, 3,
            4, 0, 0, 1, 2, 5, 5, 5, 6, 6,
            4, 7, 7, 1, 2, 5, 8, 6, 6, 9,
            4, 7, 7, 10, 10, 8, 8, 8, 11, 9,
            4, 12, 10, 10, 13, 13, 11, 11, 11, 9,
            14, 12, 12, 12, 13, 13, 15, 15, 15, 9,
            14, 14, 16, 16, 16, 17, 18, 18, 15, 19,
            20, 14, 21, 16, 22, 17, 18, 18, 19, 19,
            20, 20, 21, 22, 22, 17, 17, 23, 23, 19,
            20, 21, 21, 22, 24, 24, 24, 24, 23, 23};

        for (int i = 0; i < testGridIDs.Length; i++)
            gridIDs[i] = testGridIDs[i];

        for (int i = 100; i < gridIDs.Length; i++)
            gridIDs[i] = (gridIDs[i - 100] + 25);

        pieceCounts = new short[] { 8, 8, 6, 6, 6, 8, 8 };

        for (int i = 0; i < pieces.Length; i++) {
            int[] positions = new int[4];
            var found = 0;
            var foundShape = '.';

            for (int j = 0; j < gridIDs.Length; j++) {
                if (gridIDs[j] == i) {
                    positions[found] = j;
                    found++;
                }

                if (found >= 4) {
                    foundShape = gridShapes[j];
                    break;
                } 
            }

            pieces[i] = new TetrisPiece(i, foundShape, positions);
        }

        Debug.LogFormat("[Tricky Tetris Pieces #{0}] The module is using a fixed grid. This could either be for testing or the algorithm has failed.", moduleId);
    }


    // Chooses the palettes for each piece
    private void ChoosePalettes() {
        for (int i = 0; i < pieces.Length; i++) {
            var valid = false;

            if (i == firstPieceIndex) {
                do {
                    var rand = UnityEngine.Random.Range(0, 10);
                    var shapeIndex = shapeColorKey[pieces[i].GetShape()];

                    valid = palettes[rand].GetValidStarts()[shapeIndex];

                    if (valid) {
                        pieces[i].SetPaletteIndex(rand);
                        firstPiecePalette = rand;

                        Debug.LogFormat("[Tricky Tetris Pieces #{0}] The piece you need to press is a(n) {1} piece with the palette: {2}",
                            moduleId, firstPieceShape, palettes[firstPiecePalette].GetPaletteName());
                    }
                } while (!valid);
            }

            else {
                do {
                    var rand = UnityEngine.Random.Range(10, palettes.Length);
                    var shapeIndex = shapeColorKey[pieces[i].GetShape()];

                    valid = palettes[rand].GetValidStarts()[shapeIndex];

                    if (valid)
                        pieces[i].SetPaletteIndex(rand);
                } while (!valid);
            }

            ColorTiles(i, pieces[i].GetPaletteIndex());
        }
    }

    // Creates the nodes for each piece
    private void CreateNodes() {
        var start = firstPieceIndex;
        float[] coords = GetNodeCoords(pieces[start].GetTilePositions());
        var distance = 0.0d;

        nodes[start] = new PieceNode(start, coords, distance);

        for (int i = 0; i < nodes.Length; i++) {
            if (i != start) {
                coords = GetNodeCoords(pieces[i].GetTilePositions());

                distance = Math.Pow(Math.Abs(coords[0] - nodes[start].GetCoord(0)), 2.0d);
                distance += Math.Pow(Math.Abs(coords[1] - nodes[start].GetCoord(1)), 2.0d);

                nodes[i] = new PieceNode(i, coords, distance);
            }
        }
    }

    // Sorts the nodes for each piece
    private void SortNodes() {
        double[] distances = new double[50];

        for (int i = 0; i < distances.Length; i++) {
            distances[i] = nodes[i].GetDistance();
            nodeOrder[i] = i;
        }

        // Selection sort - https://www.geeksforgeeks.org/dsa/selection-sort-algorithm-2/
        var tempDistance = 0.0d;
        var tempOrderIndex = 0;
        var minIndex = 0;

        for (int i = 0; i < distances.Length - 1; i++) {
            minIndex = i;

            for (int j = i + 1; j < distances.Length; j++) {
                if (distances[j] < distances[minIndex])
                    minIndex = j;
            }

            if (minIndex != i) {
                tempDistance = distances[i];
                tempOrderIndex = nodeOrder[i];

                distances[i] = distances[minIndex];
                nodeOrder[i] = nodeOrder[minIndex];

                distances[minIndex] = tempDistance;
                nodeOrder[minIndex] = tempOrderIndex;
            }
        }
    }

    // Finds the tile index in the manual from the first piece
    private void GetManualIndex() {
        var initialIndex = MANUAL_INDICES[firstPiecePalette];
        var foundIndex = 0;
        var minDistance = 19;
        var currentDistance = 0;

        for (int i = 0; i < MANUAL_GRID.Length; i++) {
            if (MANUAL_GRID[i] == firstPieceShape) {
                currentDistance = Math.Abs((i % 10) - (initialIndex % 10)); // Horizontal
                currentDistance += Math.Abs((i / 10) - (initialIndex / 10)); // Vertical

                if (currentDistance < minDistance) {
                    minDistance = currentDistance;
                    foundIndex = i;
                }
            }
        }

        manualIndex = foundIndex;
        Debug.LogFormat("<Tricky Tetris Pieces #{0}> Stage 1 manual position: {1}", moduleId, GetTilePosition(manualIndex));
    }


    // Fills in the grid with the first piece palette
    private IEnumerator AdvanceStage() {
        canPress = false;
        stageTwo = true;
        halfModulesSolved = Bomb.GetSolvedModuleNames().Count() > Bomb.GetSolvableModuleNames().Count() / 2;

        if (halfModulesSolved)
            Debug.LogFormat("[Tricky Tetris Pieces #{0}] Half of the modules on the bomb have been solved. Adding 64 to the palette IDs.", moduleId);

        Audio.PlaySoundAtTransform("ttp_advance", transform);
        TimerText.text = "HURRY UP!";

        for (int i = 0; i < nodeOrder.Length; i++) {
            pieces[nodeOrder[i]].SetPaletteIndex(firstPiecePalette);
            ColorTiles(nodeOrder[i], firstPiecePalette);

            if (i % 10 == 5)
                TimerText.text = "";

            else if (i % 10 == 8)
                TimerText.text = "HURRY UP!";

            yield return new WaitForSeconds(1.0f / 30.0f);
        }

        CalculateSecondStage();
    }

    // Calculates the second correct piece
    private void CalculateSecondStage() {
        do {
            secondPalette = UnityEngine.Random.Range(0, palettes.Length);
        } while (secondPalette == firstPiecePalette);

        Debug.LogFormat("[Tricky Tetris Pieces #{0}] The new palette that appears is: {1}", moduleId, palettes[secondPalette].GetPaletteName());

        var firstPalId = palettes[firstPiecePalette].GetPaletteID();
        var secondPalId = palettes[secondPalette].GetPaletteID();

        if (halfModulesSolved) {
            firstPalId += ID_INCREASE_FACTOR;
            secondPalId += ID_INCREASE_FACTOR;
        }

        Debug.LogFormat("[Tricky Tetris Pieces #{0}] The ID numbers of the two palettes are {1} and {2}.", moduleId, firstPalId.ToString(), secondPalId.ToString());

        var newIndex = (manualIndex + secondPalId) % MANUAL_GRID.Length;
        secondPieceShape = MANUAL_GRID[newIndex];

        Debug.LogFormat("[Tricky Tetris Pieces #{0}] The new piece you need to press is a(n) {1} piece.", moduleId, secondPieceShape);

        // Checks if the new piece is present on the module
        for (int i = 0; i < SHAPE_NAMES.Length; i++) {
            if (SHAPE_NAMES[i] == secondPieceShape) {
                noSecondPiece = pieceCounts[i] == 0;

                if (noSecondPiece)
                    Debug.LogFormat("[Tricky Tetris Pieces #{0}] There are no {1} pieces on the module. Press the first piece again.", moduleId, secondPieceShape);

                break;
            }
        }

        StartCoroutine(Countdown());
    }

    // Starts the timer and slowly converts the grid to the new palette
    private IEnumerator Countdown() {
        canPress = true;
        canCountDown = true;
        moduleTimer = DEFAULT_TIMER;
        if (Bomb.GetSolvedModuleNames().Count() == 80)
            moduleTimer *= 8;

        var convertFactor = Math.Max(moduleTimer / pieces.Length, 1);
        var piecesConverted = 0;

        // Starts the music
        if (Settings.PlayMusic) {
            try {
                MainMusic.volume = GameMusicControl.GameSFXVolume;
            }

            catch (NullReferenceException) {
                MainMusic.volume = TEST_MUSIC_VOLUME;
            }

            MainMusic.Play();
        }

        // Counts down the timer
        while (canCountDown && moduleTimer > 0) {
            TimerText.text = "TIME-" + FormatTime(moduleTimer, true);

            if (moduleTimer % convertFactor == 0) {
                pieces[nodeOrder[piecesConverted]].SetPaletteIndex(secondPalette);
                ColorTiles(nodeOrder[piecesConverted], secondPalette);
                piecesConverted++;
            }

            yield return new WaitForSeconds(0.6f);
            moduleTimer--;
        }

        if (moduleTimer == 0) {
            TimerText.text = "TIME- 00";
            Debug.LogFormat("[Tricky Tetris Pieces #{0}] You ran out of time! Strike!", moduleId);
            StartCoroutine(FailModule());
        }
    }

    // Checks if the pressed time is valid
    private bool CheckValidTime(int time, int pal) {
        var matchCount = 0;
        var timeString = FormatTime(time, false);
        var palString = pal.ToString();

        for (int i = 0; i < timeString.Length; i++) {
            if (palString.Contains(timeString[i]))
                matchCount++;
        }

        return matchCount % 2 == 1;
    }


    // Solving animation
    private IEnumerator SolveModule() {
        GetComponent<KMBombModule>().HandlePass();

        canCountDown = false;
        canPress = false;
        moduleSolved = true;

        if (MainMusic.isPlaying)
            MainMusic.Stop();

        Audio.PlaySoundAtTransform("ttp_solve", transform);
        TimerText.text = " SOLVED!";

        StartCoroutine(WipeModule(false, 0));

        for (int i = 1; i < 9; i++) {
            ModuleBacking.material = ModuleBackingMaterials[i % 2];
            yield return new WaitForSeconds(0.1f);
        }
    }

    // Stage 2 strike animation
    private IEnumerator FailModule() {
        GetComponent<KMBombModule>().HandleStrike();

        canCountDown = false;
        canPress = false;

        if (MainMusic.isPlaying)
            MainMusic.Stop();

        Audio.PlaySoundAtTransform("ttp_fail", transform);

        yield return new WaitForSeconds(1.0f);

        if (Settings.AlternateStrikeSound)
            Audio.PlaySoundAtTransform("ttp_altStrike", transform);

        StartCoroutine(WipeModule(false, 2));
    }

    // Module wiping animation
    private IEnumerator WipeModule(bool filling, byte action) {
        for (int i = 19; i >= 0; i--) {
            for (int j = 0; j < 10; j++)
                TileObjects[i * 10 + j].SetActive(filling);

            yield return new WaitForSeconds(0.1f);
        }
        
        switch (action) {
            case 1: // Stage one start
                canPress = true;
                TimerText.text = "LINE-" + GenerateRandomString(); ;
                break;

            case 2: // Stage two struck
                Debug.LogFormat("[Tricky Tetris Pieces #{0}] Reseting module...", moduleId);
                ResetModule();
                break;

            default: // Nothing (or module solved)
                break;
        }
    }


    // Pressing a tile
    private void PressTile(int i) {
        TileSelectables[i].AddInteractionPunch(0.25f);

        if (canPress && !moduleSolved) {
            var pressedPalette = palettes[pieces[gridIDs[i]].GetPaletteIndex()];
            Debug.LogFormat("[Tricky Tetris Pieces #{0}] You pressed the tile at position {1}. It is part of a(n) {2} piece with the palette: {3}",
                moduleId, GetTilePosition(i), gridShapes[i], pressedPalette.GetPaletteName());

            if (stageTwo) {
                var palId = halfModulesSolved ? pressedPalette.GetPaletteID() + ID_INCREASE_FACTOR : pressedPalette.GetPaletteID();
                Debug.LogFormat("[Tricky Tetris Pieces #{0}] You pressed the tile when the timer displays {1}, and the palette's ID number is {2}.",
                    moduleId, FormatTime(moduleTimer, false), palId);

                if (noSecondPiece) { // Stage 2 - Goal shape not present
                    if (gridIDs[i] == firstPieceIndex) { // Correct piece
                        if (CheckValidTime(moduleTimer, palId)) { // Valid time
                            Debug.LogFormat("[Tricky Tetris Pieces #{0}] You pressed the correct piece at the correct time! Module solved!", moduleId);
                            StartCoroutine(SolveModule());
                        }

                        else { // Invalid time
                            Debug.LogFormat("[Tricky Tetris Pieces #{0}] The piece was pressed at an incorrect time. Strike!", moduleId);
                            StartCoroutine(FailModule());
                        }
                    }

                    else { // Wrong piece
                        Debug.LogFormat("[Tricky Tetris Pieces #{0}] That was not the correct piece. Strike!", moduleId);
                        StartCoroutine(FailModule());
                    }
                }

                else { // Stage 2 - Goal shape present
                    if (gridShapes[i] == secondPieceShape) { // Correct shape
                        if (CheckValidTime(moduleTimer, palId)) { // Valid time
                            Debug.LogFormat("[Tricky Tetris Pieces #{0}] You pressed the correct shape at the correct time! Module solved!", moduleId);
                            StartCoroutine(SolveModule());
                        }
                        
                        else { // Invalid time
                            Debug.LogFormat("[Tricky Tetris Pieces #{0}] The shape was pressed at an incorrect time. Strike!", moduleId);
                            StartCoroutine(FailModule());
                        }
                    }

                    else { // Wrong shape
                        Debug.LogFormat("[Tricky Tetris Pieces #{0}] That was not the correct shape. Strike!", moduleId);
                        StartCoroutine(FailModule());
                    }
                }
            }

            else { // Stage 1
                if (gridIDs[i] == firstPieceIndex) { // Correct
                    Debug.LogFormat("[Tricky Tetris Pieces #{0}] That was the correct piece! Let's start the timer!", moduleId);
                    StartCoroutine(AdvanceStage());
                }

                else { // Incorrect
                    Debug.LogFormat("[Tricky Tetris Pieces #{0}] That was not the correct piece. Strike!", moduleId);
                    GetComponent<KMBombModule>().HandleStrike();
                }
            }
        }
    }

    // Pressing the switch
    private void PressSwitch() {
        Switch.AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, gameObject.transform);

        if (!moduleSolved && !switchFlipped)
            StartCoroutine(AnimateSwitch());
    }

    // Animates the switch
    private IEnumerator AnimateSwitch() {
        switchFlipped = true;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSequenceMechanism, gameObject.transform);

        for (int i = 1; i <= 10; i++) {
            var newPos = i / 20.0f;

            SwitchPosition.localPosition = new Vector3(-0.25f + newPos, 1.5f, 0.0f);
            yield return new WaitForSeconds(1.0f / 30.0f);
        }
    }

    // Displays info on the side screens when highlighting a tile
    private void HighlightTile(int i) {
        if (i > -1 && switchFlipped) {
            // Shows the color codes
            var pal = pieces[gridIDs[i]].GetPaletteIndex();
            var str = "";

            switch (shapeColorKey[gridShapes[i]]) {
                case 1: str = palettes[pal].GetPaletteColor(1).ToString() + "\n" + palettes[pal].GetPaletteColor(0).ToString(); break;
                case 2: str = palettes[pal].GetPaletteColor(2).ToString() + "\n" + palettes[pal].GetPaletteColor(0).ToString(); break;
                default: str = palettes[pal].GetPaletteColor(0).ToString() + "\n" + palettes[pal].GetPaletteColor(2).ToString(); break;
            }

            ColorText.text = str;

            // Shows the shape
            switch (gridShapes[i]) {
                case 'J':
                    BlankTiles[0].enabled = true;
                    BlankTiles[2].enabled = true;
                    BlankTiles[4].enabled = true;
                    BlankTiles[13].enabled = true;
                    break;

                case 'Z':
                    BlankTiles[0].enabled = true;
                    BlankTiles[2].enabled = true;
                    BlankTiles[11].enabled = true;
                    BlankTiles[13].enabled = true;
                    break;

                case 'O':
                    BlankTiles[1].enabled = true;
                    BlankTiles[3].enabled = true;
                    BlankTiles[10].enabled = true;
                    BlankTiles[12].enabled = true;
                    break;

                case 'S':
                    BlankTiles[2].enabled = true;
                    BlankTiles[4].enabled = true;
                    BlankTiles[9].enabled = true;
                    BlankTiles[11].enabled = true;
                    break;

                case 'L':
                    BlankTiles[0].enabled = true;
                    BlankTiles[2].enabled = true;
                    BlankTiles[4].enabled = true;
                    BlankTiles[9].enabled = true;
                    break;

                case 'I':
                    BlankTiles[5].enabled = true;
                    BlankTiles[6].enabled = true;
                    BlankTiles[7].enabled = true;
                    BlankTiles[8].enabled = true;
                    break;

                default: // T
                    BlankTiles[0].enabled = true;
                    BlankTiles[2].enabled = true;
                    BlankTiles[4].enabled = true;
                    BlankTiles[11].enabled = true;
                    break;
            }
        }

        // Remove the info
        else {
            ColorText.text = "";

            for (int j = 0; j < BlankTiles.Length; j++)
                BlankTiles[j].enabled = false;
        }
    }


    // Colors the tiles on the module
    private void ColorTiles(int piece, int pal) {
        int[] positions = pieces[piece].GetTilePositions();
        var palSubentry = shapeColorKey[pieces[piece].GetShape()];

        for (int i = 0; i < positions.Length; i++)
            TileRenderers[positions[i]].material = TileMaterials[pal * 3 + palSubentry];
    }
    
    // Gets the position of a tile for logging
    private string GetTilePosition(int pos) {
        var str = "";

        switch (pos % 10) {
            case 1: str += "B"; break;
            case 2: str += "C"; break;
            case 3: str += "D"; break;
            case 4: str += "E"; break;
            case 5: str += "F"; break;
            case 6: str += "G"; break;
            case 7: str += "H"; break;
            case 8: str += "I"; break;
            case 9: str += "J"; break;
            default: str += "A"; break;
        }

        str += (pos / 10) + 1;

        return str;
    }

    // Formats the time on the display
    private string FormatTime(int time, bool whitespace) {
        var str = "";

        if (time > 999)
            str = "999";

        else if (time >= 100)
            str = time.ToString();

        else if (time >= 10)
            str = " " + time.ToString();

        else
            str = " 0" + time.ToString();

        if (whitespace || time >= 100)
            return str;

        else
            return str.Substring(1);
    }

    // Gets the average of four positions
    private float[] GetNodeCoords(int[] positions) {
        float[] coords = { 0.0f, 0.0f };

        for (int i = 0; i < positions.Length; i++) {
            coords[0] += positions[i] % 10; // Horizontal
            coords[1] += positions[i] / 10; // Vertical
        }

        coords[0] /= positions.Length;
        coords[1] /= positions.Length;

        return coords;
    }

    // Generates a random string of letters and numbers for the timer display
    private string GenerateRandomString() {
        var str = UnityEngine.Random.Range(100, 1000).ToString();

        return str;
    }
}