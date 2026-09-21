using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;

public class InputSequential : MonoBehaviour
{
    #region Constants - Magic number'ları topladık
    private static readonly int[] FRIENDLY_STEPS =
    {
        1,
        2,
        3,
        4,
        5,
        6,
        8,
        10,
        12,
        15,
        20,
        25,
        30,
        40,
        50,
        75,
        100,
        200,
        500,
        1000,
    };
    private const int MAX_INPUT_FIELD_COUNT = 8;
    private const int CAP_LEVEL = 50;
    private const int BLOCK_SIZE = 10;
    private const float RANGE_MULTIPLIER = 3f; // idealStep hesabı için
    private const float QUARTER_RATIO = 0.25f;
    private const float HALF_RATIO = 0.5f;
    #endregion
    private StemGameManager stem;

    [SerializeField]
    private GameObject inputFieldPrefab;

    [SerializeField]
    private GameObject numberImagePrefab;
    private GameObject optionalPrefab;
    public bool isDynamicMode;
    public bool isReverseMode;
    public bool isRandomMode;
    public bool isEvent;
    public GameObject objectParent;
    public Vector2 numberRange;
    public int levelIndex = 0;
    private int inputFieldCount;
    public int step;
    private TextMeshProUGUI yonergeText;
    private GameObject SoundFX;
    private GameObject WrongSFX;
    private GameObject WinSFX;
    private GameObject WinSFXFinal;

    private int startValue,
        endValue;

    public List<TMP_InputField> inputFields = new List<TMP_InputField>();

    void Start()
    {
        stem = FindAnyObjectByType<StemGameManager>();
        if (stem != null)
        {
            InitializeComponents();
        }
    }

    private void InitializeComponents()
    {
        yonergeText = stem.yonergeText;
        WinSFX = stem.WinSFX;
        WrongSFX = stem.WrongSFX;
        WinSFXFinal = stem.WinSFXFinal;

        levelIndex = stem.levelIndex;
        isDynamicMode = stem.isDynamicMode;
        isRandomMode = stem.isRandom;
        isReverseMode = stem.isReverseMode;
        step = stem.step;
        isEvent = stem.isEvent;
        numberRange = stem.numberRangeXY;

        numberImagePrefab = stem.etcPrefab[0];
        inputFieldPrefab = stem.etcPrefab[1];

        if (stem.opsionelPrefab != null)
        {
            optionalPrefab = stem.opsionelPrefab;
        }

        if (stem.trueObjects == null || stem.trueObjects.Length == 0 || stem.trueObjects[0] == null)
        {
            Debug.LogError("StemGameManager.trueObjects boş! En az bir parent obje atayın.");
            return;
        }

        objectParent = stem.trueObjects[0];
        ApplyDifficultyWithSequentialInput(levelIndex);
        SetupInputFields();
        UpdateYonergeText();
    }

    private void SetupInputFields()
    {
        foreach (var field in inputFields)
        {
            if (field != null)
                field.onEndEdit.RemoveAllListeners();
        }
        inputFields.Clear();

        foreach (Transform child in objectParent.transform)
            Destroy(child.gameObject);

        if (!isDynamicMode)
        {
            List<int> sequence = GeneratePatternSequence();
            int totalCount = sequence.Count;

            // --- CONST & INPUT HESABI ---
            List<int> constIndices = new List<int> { 0, totalCount - 1 }; // her zaman baş & son

            // Input sayısı büyüdükçe ipucu serpiştir
            if (inputFieldCount >= 3)
            {
                constIndices.Add(totalCount / 2); // ortadaki ipucu
            }
            if (inputFieldCount >= 6)
            {
                constIndices.Add(totalCount / 3); // 1/3 nokta
                constIndices.Add(2 * totalCount / 3); // 2/3 nokta
            }

            // Tekrarları sil & sırala
            constIndices = new HashSet<int>(constIndices).ToList();
            constIndices.Sort();

            // Çok küçük dizilerde input kalmazsa → sadece baş/son bırak
            if (constIndices.Count >= totalCount)
                constIndices = new List<int> { 0, totalCount - 1 };

            // --- NESNELERİ OLUŞTUR ---
            for (int i = 0; i < totalCount; i++)
            {
                bool isConst = constIndices.Contains(i);

                if (isConst)
                {
                    var numObj = Instantiate(numberImagePrefab, objectParent.transform);
                    numObj.GetComponentInChildren<TextMeshProUGUI>().text = sequence[i].ToString();
                }
                else
                {
                    var inputObj = Instantiate(inputFieldPrefab, objectParent.transform);
                    TMP_InputField inputField = inputObj.GetComponent<TMP_InputField>();
                    if (inputField != null)
                    {
                        inputFields.Add(inputField);
                        int correctValue = sequence[i];
                        inputField.onEndEdit.AddListener(
                            (string value) => CheckPatternInput(inputField, correctValue)
                        );
                    }
                }
            }

            Debug.Log(
                $"[LEVEL {levelIndex}] TotalCount={totalCount}, ConstIndices=[{string.Join(",", constIndices)}], InputCount={inputFields.Count}"
            );
        }
        else
        {
            // Dynamic mode (değişmedi)
            var firstObj = Instantiate(numberImagePrefab, objectParent.transform);
            firstObj.GetComponentInChildren<TextMeshProUGUI>().text = startValue.ToString();

            for (int i = 0; i < inputFieldCount; i++)
            {
                var inputObj = Instantiate(inputFieldPrefab, objectParent.transform);
                TMP_InputField inputField = inputObj.GetComponent<TMP_InputField>();
                if (inputField != null)
                {
                    inputFields.Add(inputField);
                    inputField.onEndEdit.AddListener(
                        (string value) => CheckSequentialDynamicInput(inputField)
                    );
                }
            }

            var lastObj = Instantiate(numberImagePrefab, objectParent.transform);
            lastObj.GetComponentInChildren<TextMeshProUGUI>().text = endValue.ToString();
        }

        if (optionalPrefab != null)
        {
            Instantiate(optionalPrefab, objectParent.transform);
        }
        else
        {
            // Decorative attachments are optional in this edition.
        }

        // Native TMP input fields use the system keyboard.
    }

    private List<int> GeneratePatternSequence()
    {
        List<int> sequence = new List<int>();
        int totalCount = inputFieldCount + 2; // baş ve son dahil

        for (int i = 0; i < totalCount; i++)
        {
            int val;
            if (!isReverseMode)
                val = startValue + i * step;
            else
                val = startValue - i * step;

            // clamp to endValue (aşarsa sona sabitle)
            if (!isReverseMode)
                val = Mathf.Min(val, endValue);
            else
                val = Mathf.Max(val, endValue);

            sequence.Add(val);
        }

        // güvenlik
        sequence[0] = startValue;
        sequence[sequence.Count - 1] = endValue;

        return sequence;
    }

    private void ApplyDifficultyWithSequentialInput(int levelIndex)
    {
        int clampedIndex = Mathf.Min(levelIndex, 100);

        // Random mod varsa reverse rastgele seç
        if (isRandomMode)
            isReverseMode = Random.value > 0.5f;

        // --- 50+ LEVEL SABİT ZORLUK ---
        if (clampedIndex >= CAP_LEVEL)
        {
            inputFieldCount = MAX_INPUT_FIELD_COUNT;
            numberRange = new Vector2(0, 9999);

            CalculateStepAndValues();
            Debug.Log(
                $"[LEVEL {levelIndex}] (CAP) inputFieldCount={inputFieldCount}, range=({numberRange.x},{numberRange.y}), start={startValue}, end={endValue}, step={step}, reverse={isReverseMode}"
            );
            return;
        }

        // --- NORMAL LEVEL HESAPLAMASI ---

        int groupIndex = clampedIndex / BLOCK_SIZE;
        int inputFieldCountMin = 1 + groupIndex;
        int inputFieldCountMax = Mathf.Min(inputFieldCountMin + 2, MAX_INPUT_FIELD_COUNT);

        float t = (clampedIndex % BLOCK_SIZE) / (float)(BLOCK_SIZE - 1);
        float wave = (Mathf.Sin(t * Mathf.PI) + 1f) * 0.5f; // 0..1 dalga

        // inputFieldCount randomize
        int inputFieldCountLower = Mathf.FloorToInt(inputFieldCountMin);
        int inputFieldCountUpper = Mathf.Min(
            Mathf.CeilToInt(inputFieldCountMin + wave * (inputFieldCountMax - inputFieldCountMin)),
            inputFieldCountMax
        );
        inputFieldCount = Random.Range(inputFieldCountLower, inputFieldCountUpper + 1);

        // Range bloklara göre
        int rangeMaxBlock = groupIndex switch
        {
            0 => 50,
            1 => 100,
            2 => 500,
            3 => 1000,
            4 => 5000,
            _ => 9999,
        };
        int rangeMin = 0;
        int rangeMax = Mathf.Max(1, Mathf.RoundToInt(rangeMaxBlock * wave));
        numberRange = new Vector2(rangeMin, rangeMax);

        CalculateStepAndValues();

        Debug.Log(
            $"[LEVEL {levelIndex}] inputFieldCount={inputFieldCount}, range=({numberRange.x},{numberRange.y}), start={startValue}, end={endValue}, step={step}, reverse={isReverseMode}"
        );
    }

    private void CalculateStepAndValues()
    {
        int totalElements = inputFieldCount + 2;
        int totalSteps = totalElements - 1;
        int rangeSize = (int)(numberRange.y - numberRange.x);

        // 1. Step calculation
        int idealStep = Mathf.Max(1, Mathf.RoundToInt(rangeSize / (totalSteps * RANGE_MULTIPLIER)));
        step = ChooseFriendlyStep(idealStep);

        // 2. Sequence span calculation
        int sequenceSpan = step * totalSteps;

        // 3. Adjust step if needed
        while (sequenceSpan > rangeSize && step > 1)
        {
            step = ChooseSmallerFriendlyStep(step);
            sequenceSpan = step * totalSteps;
        }

        // 4. Calculate start/end values with better distribution
        CalculateStartEndValues(sequenceSpan, rangeSize);

        // 5. Safety clamps
        ApplySafetyClamps(sequenceSpan);

        Debug.Log(
            $"[CalculateStepAndValues] sequenceSpan={sequenceSpan}, startValue={startValue}, endValue={endValue}, step={step}, reverse={isReverseMode}"
        );
    }

    private void ApplySafetyClamps(int sequenceSpan)
    {
        if (!isReverseMode)
        {
            startValue = Mathf.Clamp(
                startValue,
                (int)numberRange.x,
                (int)numberRange.y - sequenceSpan
            );
            endValue = Mathf.Clamp(endValue, (int)numberRange.x + sequenceSpan, (int)numberRange.y);
        }
        else
        {
            startValue = Mathf.Clamp(
                startValue,
                (int)numberRange.x + sequenceSpan,
                (int)numberRange.y
            );
            endValue = Mathf.Clamp(endValue, (int)numberRange.x, (int)numberRange.y - sequenceSpan);
        }
    }

    private void CalculateStartEndValues(int sequenceSpan, int rangeSize)
    {
        if (!isReverseMode)
        {
            int maxPossibleStart = (int)numberRange.y - sequenceSpan;
            int minPossibleStart = (int)numberRange.x;

            int safeMinStart = Mathf.Max(
                minPossibleStart,
                (int)numberRange.x + Mathf.RoundToInt(rangeSize * QUARTER_RATIO)
            );
            int safeMaxStart = Mathf.Min(
                maxPossibleStart,
                (int)numberRange.y - Mathf.RoundToInt(rangeSize * HALF_RATIO)
            );

            startValue = Random.Range(safeMinStart, safeMaxStart + 1);
            endValue = startValue + sequenceSpan;
        }
        else
        {
            int minPossibleStart = (int)numberRange.x + sequenceSpan;
            int maxPossibleStart = (int)numberRange.y;

            int safeMinStart = Mathf.Max(
                minPossibleStart,
                (int)numberRange.x + Mathf.RoundToInt(rangeSize * HALF_RATIO)
            );
            int safeMaxStart = Mathf.Min(
                maxPossibleStart,
                (int)numberRange.y - Mathf.RoundToInt(rangeSize * QUARTER_RATIO)
            );

            startValue = Random.Range(safeMinStart, safeMaxStart + 1);
            endValue = startValue - sequenceSpan;
        }
    }

    private static int ChooseSmallerFriendlyStep(int currentStep)
    {
        for (int i = FRIENDLY_STEPS.Length - 1; i >= 0; i--)
        {
            if (FRIENDLY_STEPS[i] < currentStep)
                return FRIENDLY_STEPS[i];
        }
        return 1;
    }

    private static int ChooseFriendlyStep(int target)
    {
        foreach (var step in FRIENDLY_STEPS)
        {
            if (step >= target)
                return step;
        }
        return FRIENDLY_STEPS[FRIENDLY_STEPS.Length - 1];
    }

    // Input kontrol ve global tutarlılık
    private void CheckSequentialDynamicInput(TMP_InputField currentField)
    {
        if (currentField.readOnly) return;
        stem.DestroyAllSFX();

        if (string.IsNullOrEmpty(currentField.text))
            return;

        if (!TryParseUserInput(currentField.text, out int val))
        {
            RejectInput(currentField);
            return;
        }

        int index = inputFields.IndexOf(currentField);
        if (index < 0)
        {
            Debug.LogError("currentField inputFields listesinde bulunamadı!");
            return;
        }

        int? leftValue = null;
        for (int i = index - 1; i >= 0; i--)
        {
            if (TryParseUserInput(inputFields[i].text, out int parsed))
            {
                leftValue = parsed;
                break;
            }
        }
        if (leftValue == null && objectParent.transform.childCount > 0)
        {
            var leftTMP = objectParent
                .transform.GetChild(0)
                .GetComponentInChildren<TextMeshProUGUI>();
            if (leftTMP != null && int.TryParse(leftTMP.text, out int parsed))
                leftValue = parsed;
        }

        int? rightValue = null;
        for (int i = index + 1; i < inputFields.Count; i++)
        {
            if (TryParseUserInput(inputFields[i].text, out int parsed))
            {
                rightValue = parsed;
                break;
            }
        }
        if (rightValue == null && objectParent.transform.childCount > 0)
        {
            var rightTMP = objectParent
                .transform.GetChild(objectParent.transform.childCount - 1)
                .GetComponentInChildren<TextMeshProUGUI>();
            if (rightTMP != null && int.TryParse(rightTMP.text, out int parsed))
                rightValue = parsed;
        }

        // Boş komşular varsa kontrolü ertele
        if (!leftValue.HasValue && !rightValue.HasValue)
            return;

        // Kontrol: sol < val < sağ
        if (!isReverseMode)
        {
            if (
                (leftValue.HasValue && val <= leftValue)
                || (rightValue.HasValue && val >= rightValue)
            )
            {
                RejectInput(currentField);
                return;
            }
        }
        else
        {
            if (
                (leftValue.HasValue && val >= leftValue)
                || (rightValue.HasValue && val <= rightValue)
            )
            {
                RejectInput(currentField);
                return;
            }
        }

        if (!ValidateGlobalConsistency())
        {
            RejectInput(currentField);
            return;
        }

        currentField.readOnly = true;
        currentField.enabled = false;
        Instantiate(WinSFX).name = "WinSFX";

        bool allFilled = true;
        foreach (var field in inputFields)
        {
            if (string.IsNullOrEmpty(field.text))
            {
                allFilled = false;
                break;
            }
        }

        if (allFilled)
        {
            TriggerFinale();
            StartCoroutine(ResetLevelTransitionWhenSfxEnds());
        }
    }

    private bool ValidateGlobalConsistency()
    {
        int? lastValue = null;
        int lastIndex = -1;
        int totalCount = objectParent.transform.childCount;

        for (int i = 0; i < totalCount; i++)
        {
            int? val = null;
            TMP_InputField field = objectParent
                .transform.GetChild(i)
                .GetComponent<TMP_InputField>();
            if (field != null && TryParseUserInput(field.text, out int parsedField))
                val = parsedField;

            if (val == null)
            {
                TextMeshProUGUI text = objectParent
                    .transform.GetChild(i)
                    .GetComponentInChildren<TextMeshProUGUI>();
                if (text != null && int.TryParse(text.text, out int parsedText))
                    val = parsedText;
            }

            if (val != null)
            {
                if (lastValue != null)
                {
                    int gap = i - lastIndex - 1;
                    int diff = !isReverseMode
                        ? val.Value - lastValue.Value - 1
                        : lastValue.Value - val.Value - 1;
                    if (diff < gap)
                        return false;
                }
                lastValue = val;
                lastIndex = i;
            }
        }
        return true;
    }

    private void CheckPatternInput(TMP_InputField field, int correctValue)
    {
        if (field.readOnly) return;
        if (string.IsNullOrEmpty(field.text))
            return;

        stem.DestroyAllSFX();

        if (!TryParseUserInput(field.text, out int val))
        {
            RejectInput(field);
            return;
        }

        if (val == correctValue)
        {
            field.readOnly = true;
            field.enabled = false;
            Instantiate(WinSFX).name = "WinSFX";
        }
        else
        {
            RejectInput(field);
        }

        // Hepsi doldu mu?
        bool allCorrect = true;
        foreach (var f in inputFields)
        {
            if (string.IsNullOrEmpty(f.text) || !f.readOnly)
            {
                allCorrect = false;
                break;
            }
        }

        if (allCorrect)
        {
            TriggerFinale();
            StartCoroutine(ResetLevelTransitionWhenSfxEnds());
        }
    }

    private bool TryParseUserInput(string text, out int value)
    {
        if (stem != null && !stem.requireCanonicalIntegerInput)
            return int.TryParse(text, out value);

        return TryParseCanonicalInteger(text, out value);
    }

    internal static bool TryParseCanonicalInteger(string text, out int value)
    {
        value = default;

        return !string.IsNullOrEmpty(text)
            && int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value
            )
            && text == value.ToString(CultureInfo.InvariantCulture);
    }

    private void RejectInput(TMP_InputField field)
    {
        field.text = "";
        Instantiate(WrongSFX).name = "WrongSFX";
    }

    private void UpdateYonergeText()
    {
        if (isDynamicMode)
        {
            yonergeText.text = isReverseMode ? "Büyükten küçüğe sırala" : "Küçükten büyüğe sırala";
        }
        else
        {
            string direction = isReverseMode ? "azalan" : "artan";
            yonergeText.text = $"{step}’er {direction} şekilde doldur";
        }
    }

    private void TriggerFinale()
    {
        if (SoundFX != null)
        {
            Destroy(SoundFX);
            SoundFX = null;
        }
        SoundFX = Instantiate(WinSFXFinal);
        SoundFX.name = "WinSFXFinal";
    }

    private IEnumerator ResetLevelTransitionWhenSfxEnds()
    {
        if (isEvent)
        {
            Debug.Log("EventMode Aktif, tren animasyonu + win sesi aynı anda başlıyor");
            StartCoroutine(PlayTrainAnimation());
        }
        // SoundFX nesnesi ve AudioSource bileşeni var mı kontrol et
        if (SoundFX != null)
        {
            var audioSource = SoundFX.GetComponent<AudioSource>();

            // Eğer AudioSource varsa ve bir ses çalıyorsa, bitmesini bekle
            if (audioSource != null && audioSource.isPlaying)
            {
                // isPlaying false olana kadar her karede bekle
                while (audioSource.isPlaying)
                {
                    yield return null;
                }
            }
        }

        if (SoundFX != null)
        {
            Destroy(SoundFX);
            SoundFX = null;
        }

        levelIndex++;
        stem.FinalAnswer();

        ApplyDifficultyWithSequentialInput(levelIndex);

        UpdateYonergeText();

        SetupInputFields();
    }

    private IEnumerator PlayTrainAnimation() { yield break; }
}


