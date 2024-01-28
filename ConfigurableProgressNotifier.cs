using LightBDD.Core.ExecutionContext;
using LightBDD.Core.Formatting;
using LightBDD.Core.Metadata;
using LightBDD.Core.Notification;
using LightBDD.Core.Notification.Events;
using LightBDD.Core.Results;
using LightBDD.Core.Results.Parameters.Tabular;
using LightBDD.Core.Results.Parameters.Trees;
using LightBDD.XUnit2;

namespace LightBDD.Contrib.ProgressNotifierEnhancements;

/// <summary>
/// ProgressNotifier that avoids having the output of the tests repeat the step name each time
/// </summary>
public class ConfigurableProgressNotifier : IProgressNotifier
{
    private readonly Action<string> _onNotify;

    private readonly Action<string> _defaultOnNotify = x =>
        ScenarioExecutionContext.GetCurrentScenarioFixtureIfPresent<ITestOutputProvider>()?.TestOutput.WriteLine(x);

    /// <summary>
    /// When set to false it will not write an additional line saying "Passed after XXXms" but will continue to write a message if it fails etc
    /// </summary>
    public bool WriteSuccessMessageForBasicSteps { get; set; }

    /// <summary>
    /// Include the final step number in each step notification.  eg It will say "STEP 1.3:" instead of "STEP 1.3/1.5" (where 1.5 is the final step) 
    /// </summary>
    public bool ShowFinalStepWithEachStep { get; set; }

    /// <summary>
    /// The number of spaces to indent each Sub-Step.
    /// Default is '4'.
    /// </summary>
    public int IndentLength { get; set; } = 4;

    /// <summary>
    /// IncludeAsPrefix: Includes the word 'STEP' and step number as a prefix with a colon to each notification eg 'Step 1.1: Given this thing'
    /// IncludeAsSuffix: Includes the word 'STEP' and step number as a suffix in brackets to each notification eg 'Given this thing (Step 1.1)'
    /// Exclude: Don't include the word 'STEP' and step number in each notification
    /// </summary>
    public StepWordAndStepNumberBehaviour StepWordAndStepNumberOnStart { get; set; } = StepWordAndStepNumberBehaviour.IncludeAsSuffix;

    /// <summary>
    /// IncludeAsPrefix: Includes the word 'STEP' and step number as a prefix with a colon to each notification eg 'Step 1.1: Given this thing'
    /// IncludeAsSuffix: Includes the word 'STEP' and step number as a suffix in brackets to each notification eg 'Given this thing (Step 1.1)'
    /// Exclude: Don't include the word 'STEP' and step number in each notification
    /// </summary>
    public StepWordAndStepNumberBehaviour StepWordAndStepNumberOnFinish { get; set; } = StepWordAndStepNumberBehaviour.IncludeAsSuffix;

    /// <summary>
    /// Includes the step name in the notification when the step finishes
    /// </summary>
    public bool IncludeStepNameOnFinish { get; set; } = false;

    /// <summary>
    /// Include '...' after each step name in the step start notification.
    /// Defaults to false.
    /// </summary>
    public bool IncludeEllipsisAfterStep { get; set; } = false;

    /// <summary>
    /// Initializes the notifier with <paramref name="onNotify"/> actions that will be used to delegate the rendered notification text.
    /// </summary>
    /// <param name="onNotify"></param>
    public ConfigurableProgressNotifier(params Action<string>[] onNotify)
    {
        _onNotify = onNotify.Any() ? onNotify.Aggregate((current, next) => current + next) : _defaultOnNotify;
    }

    /// <summary>
    /// Notifies that scenario has started.
    /// </summary>
    /// <param name="scenario">Scenario info.</param>
    public virtual void NotifyScenarioStart(IScenarioInfo scenario)
    {
        _onNotify($"SCENARIO: {FormatLabels(scenario.Labels)}{scenario.Name}");
    }

    /// <summary>
    /// Notifies that scenario has finished.
    /// </summary>
    /// <param name="scenario">Scenario result.</param>
    public virtual void NotifyScenarioFinished(IScenarioResult scenario)
    {
        var scenarioText = scenario.ExecutionTime != null
            ? $"  SCENARIO RESULT: {scenario.Status} after {scenario.ExecutionTime.Duration.FormatPretty()}"
            : $"  SCENARIO RESULT: {scenario.Status}";

        var scenarioDetails = !string.IsNullOrWhiteSpace(scenario.StatusDetails)
            ? $"{Environment.NewLine}    {scenario.StatusDetails.Replace(Environment.NewLine, Environment.NewLine + "    ")}"
            : string.Empty;

        _onNotify(scenarioText + scenarioDetails);
    }

    /// <summary>
    /// Notifies that step has started.
    /// </summary>
    /// <param name="step">Step info.</param>
    public virtual void NotifyStepStart(IStepInfo step)
    {
        var indentPrefix = GetIndentPrefix(step);
        var notification = $"{indentPrefix}";

        var stepWordAndStepNumber = GetStepWordAndStepNumber(step);

        if (StepWordAndStepNumberOnStart == StepWordAndStepNumberBehaviour.IncludeAsPrefix)
            notification += stepWordAndStepNumber + ": ";

        notification += step.Name;

        if (IncludeEllipsisAfterStep)
            notification += "...";

        if (StepWordAndStepNumberOnStart == StepWordAndStepNumberBehaviour.IncludeAsSuffix)
            notification += $" ({stepWordAndStepNumber})";

        _onNotify(notification);
    }

    private string GetStepWordAndStepNumber(IStepInfo step) => $"STEP {step.GroupPrefix}{step.Number}{(ShowFinalStepWithEachStep ? $"/{step.GroupPrefix}{step.Total}" : "")}";

    /// <summary>
    /// Notifies that step has finished.
    /// </summary>
    /// <param name="step">Step result.</param>
    public virtual void NotifyStepFinished(IStepResult step)
    {
        var indentPrefix = GetIndentPrefix(step.Info);
        var report = new List<string>();

        if (WriteSuccessMessageForBasicSteps || step.Status != ExecutionStatus.Passed)
        {
            var notification = $"{indentPrefix}";
            var stepWordAndStepNumber = GetStepWordAndStepNumber(step.Info);

            if (StepWordAndStepNumberOnFinish == StepWordAndStepNumberBehaviour.IncludeAsPrefix)
                notification += stepWordAndStepNumber + ": ";

            if (IncludeStepNameOnFinish)
                notification += step.Info.Name;

            notification += "  ";

            if (StepWordAndStepNumberOnFinish != StepWordAndStepNumberBehaviour.IncludeAsPrefix && indentPrefix != "")
                notification += "  =>";

            notification += $" ({step.Status} after {step.ExecutionTime.Duration.FormatPretty()})";

            if (StepWordAndStepNumberOnStart == StepWordAndStepNumberBehaviour.IncludeAsSuffix)
                notification += $" ({stepWordAndStepNumber})";

            report.Add(notification);
        }

        foreach (var parameter in step.Parameters)
        {
            switch (parameter.Details)
            {
                case ITabularParameterDetails table:
                    report.Add($"{indentPrefix}    {parameter.Name}:");
                    report.Add(new TextTableRenderer(table).Render($"{indentPrefix}    "));
                    break;
                case ITreeParameterDetails tree:
                    report.Add($"{indentPrefix}    {parameter.Name}:");
                    report.Add(TextTreeRenderer.Render($"{indentPrefix}    ", tree));
                    break;
            }
        }

        var message = string.Join(Environment.NewLine, report).Trim();

        if (!string.IsNullOrWhiteSpace(message))
            _onNotify(string.Join(Environment.NewLine, report).Trim());
    }

    /// <summary>
    /// Notifies that step has been commented.
    /// </summary>
    /// <param name="step">Step info.</param>
    /// <param name="comment">Comment.</param>
    public virtual void NotifyStepComment(IStepInfo step, string comment)
    {
        _onNotify($"{GetIndentPrefix(step)}    => /* {comment} */");
    }

    /// <summary>
    /// Notifies that feature has started.
    /// </summary>
    /// <param name="feature">Feature info.</param>
    public virtual void NotifyFeatureStart(IFeatureInfo feature)
    {
        _onNotify($"FEATURE: {FormatLabels(feature.Labels)}{feature.Name}{FormatDescription(feature.Description)}");
    }

    /// <summary>
    /// Notifies that feature has finished.
    /// </summary>
    /// <param name="feature">Feature result.</param>
    public virtual void NotifyFeatureFinished(IFeatureResult feature)
    {
        _onNotify($"FEATURE FINISHED: {feature.Info.Name}");
    }

    /// <inheritdoc />
    public virtual void Notify(ProgressEvent e)
    {
        switch (e)
        {
            case FeatureFinished featureFinished:
                NotifyFeatureFinished(featureFinished.Result);
                break;
            case FeatureStarting featureStarting:
                NotifyFeatureStart(featureStarting.Feature);
                break;
            case ScenarioFinished scenarioFinished:
                NotifyScenarioFinished(scenarioFinished.Result);
                break;
            case ScenarioStarting scenarioStarting:
                NotifyScenarioStart(scenarioStarting.Scenario);
                break;
            case StepCommented stepCommented:
                NotifyStepComment(stepCommented.Step, stepCommented.Comment);
                break;
            case StepFinished stepFinished:
                NotifyStepFinished(stepFinished.Result);
                break;
            case StepStarting stepStarting:
                NotifyStepStart(stepStarting.Step);
                break;
            case StepFileAttached stepFileAttached:
                NotifyStepFileAttached(stepFileAttached.Step, stepFileAttached.Attachment);
                break;
        }
    }

    protected virtual void NotifyStepFileAttached(IStepInfo step, FileAttachment attachment)
    {
        _onNotify($"    => 🔗{attachment.Name}: {attachment.FilePath}");
    }

    protected virtual string FormatDescription(string description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : $"{Environment.NewLine}  {description.Replace(Environment.NewLine, Environment.NewLine + "  ")}";
    }

    protected virtual string FormatLabels(IEnumerable<string> labels)
    {
        var joinedLabels = string.Join("][", labels);
        if (joinedLabels != string.Empty)
            joinedLabels = "[" + joinedLabels + "] ";
        return joinedLabels;
    }

    protected virtual string GetIndentPrefix(IStepInfo step)
    {
        var parent = step.Parent;
        var prefix = "";

        if(!GivenWhenThenAndBut.Any(standardPrefix => step.Name.ToString().StartsWith(standardPrefix)))
            prefix += string.Concat(Enumerable.Repeat(" ", IndentLength));

        while (parent is IStepInfo parentStep)
        {
            prefix += string.Concat(Enumerable.Repeat(" ", IndentLength));
            parent = parentStep.Parent;
        }

        prefix += string.Concat(Enumerable.Repeat(" ", IndentLength));

        return prefix;
    }

    private static readonly string[] GivenWhenThenAndBut = { "GIVEN", "WHEN", "THEN", "AND", "BUT" };
}