namespace BrewLib.Graphics.Shaders;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public class ShaderContext
{
    readonly Dictionary<ShaderVariable, HashSet<ShaderVariable>> dependencies = [];

    readonly HashSet<ShaderVariable> usedVariables = [], flowVariables = [];

    bool canReceiveCommands;

    StringBuilder code;
    ShaderVariable[] dependantVariables;
    int lastId;
    string nextGenericName => $"_tmp_{lastId++:000}";

    public VertexDeclaration VertexDeclaration { get; set; }

    public void RecordDependency(ShaderVariable referencedVariable)
    {
        if (dependantVariables is null)
            throw new InvalidOperationException("Cannot reference variables while dependencies aren't defined");

        flowVariables.Add(referencedVariable);

        if (dependantVariables is null) return;

        foreach (var dependentVariable in dependantVariables)
        {
            if (referencedVariable == dependentVariable) continue;

            if (!dependencies.TryGetValue(dependentVariable, out var existingDependencies))
                existingDependencies = dependencies[dependentVariable] = [];

            existingDependencies.Add(referencedVariable);
        }
    }

    public void MarkUsedVariables(Action action, params ShaderVariable[] outputVariables)
    {
        if (canReceiveCommands)
            throw new InvalidOperationException(code is null ?
                "Already marking used variables" :
                "Can't mark used variables while generate code");

        canReceiveCommands = true;
        action();
        canReceiveCommands = false;

        foreach (var flowVariable in flowVariables) markUsed(flowVariable);
        foreach (var t in outputVariables) markUsed(t);
    }

    public void GenerateCode(StringBuilder code, Action action)
    {
        if (canReceiveCommands)
            throw new InvalidOperationException(this.code is not null ?
                "Already generating code" :
                "Can't generate code while mark used variables");

        this.code = code;
        canReceiveCommands = true;
        action();
        this.code = null;
        canReceiveCommands = false;
    }

    public bool Uses(ShaderVariable variable) => usedVariables.Contains(variable);

    public ShaderVariable Declare(string shaderTypeName, Func<string> expression = null)
    {
        checkCanReceiveCommands();

        ShaderVariable variable = new(this, nextGenericName, shaderTypeName);
        assign(variable, expression, true);
        return variable;
    }

    public void Assign(ShaderVariable result, Func<string> expression, string components = null)
        => assign(result, expression, false, components);

    public void Assign(ShaderVariable result, ShaderVariable value, string components = null)
        => assign(result, value.Ref.ToString, false, components);

    public void Dependant(Func<string> expression, params ShaderVariable[] dependantVariables)
    {
        checkCanReceiveCommands();

        var previousDependentVariables = this.dependantVariables;
        this.dependantVariables = dependantVariables;

        if (code is not null) code.AppendLine(CultureInfo.InvariantCulture, $"{expression()};");
        else expression();

        this.dependantVariables = previousDependentVariables;
    }

    public void Comment(string line)
    {
        checkCanReceiveCommands();
        line = string.Join("\n// ", line.Split('\n'));
        code?.AppendLine(CultureInfo.InvariantCulture, $"\n// {line}\n");
    }

    void assign(ShaderVariable result, Func<string> expression, bool declare, string components = null)
    {
        checkCanReceiveCommands();

        ArgumentNullException.ThrowIfNull(result);
        if (declare && components is not null)
            throw new InvalidOperationException("Cannot set components when declaring a variable");

        if (expression is not null)
            Dependant(() => declare ? $"{result.ShaderTypeName} {result.Ref} = {expression()}" :
                    components is not null ? $"{result.Ref}.{components} = {expression()}" :
                    $"{result.Ref} = {expression()}",
                result);

        else if (declare) code?.AppendLine(CultureInfo.InvariantCulture, $"{result.ShaderTypeName} {result.Name};");
        else throw new ArgumentNullException(nameof(expression));
    }

    void markUsed(ShaderVariable var)
    {
        if (!usedVariables.Add(var)) return;
        if (!dependencies.TryGetValue(var, out var variableDependencies)) return;

        foreach (var dependency in variableDependencies) markUsed(dependency);
    }

    void checkCanReceiveCommands()
    {
        if (!canReceiveCommands)
            throw new InvalidOperationException(
                $"Cannot receive commands outside of {nameof(MarkUsedVariables)} or {nameof(GenerateCode)}");
    }
}