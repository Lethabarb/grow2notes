using System.Reflection;
using System.Text.Json;
using Grow2Notes.Tests.Features.Mcp.Planted;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Features.Mcp.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// What a tool may return is an allow-list and a deny-list (mcp-server.md §6.3 rule 2, S07.02.06), so that no tool can
/// send a date of birth or anything a note holds. The allow-list: every tool's output type is in
/// <c>Grow2Notes.Web.Features.Mcp.Contracts</c>, and its leaves are only strings, booleans, integers and GUIDs, which
/// rules out every date. The deny-list: no property name that is, or holds, a name that notes use. The rules are
/// walked over the output types, over the output schemas production lists, and over what each tool really returns.
/// Planted violations show that each rule refuses what it is there to refuse.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ContractRulesTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string ContractsNamespace = "Grow2Notes.Web.Features.Mcp.Contracts";

    private const string Revision = "2025-11-25";

    private const string OnTheDenyList = "the name is on the deny-list";

    private const string NotALeaf = "is not a string, bool, int or Guid";

    private const string PlantedSchema = """
        {
          "type": "object",
          "properties": {
            "versions": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": { "flagReason": { "type": "string" } }
              }
            }
          }
        }
        """;

    // Exact names, some of which the fragments do not cover, and the fragments, which are matched in any case: a name
    // that holds one is refused whatever surrounds it (isEveryNote is allowed, toFlagCount is not).
    private static readonly string[] DeniedNames =
    [
        "narrative", "flagReason", "isFlagged", "isTicked", "isPicked", "comment", "noteDate", "versionNumber",
        "dateOfBirth", "dob",
    ];

    private static readonly string[] DeniedFragments =
        ["narrative", "flag", "tick", "pick", "comment", "birth", "draft", "notedate"];

    private static readonly string[] Combinators = ["anyOf", "oneOf", "allOf"];

    // Each walk gives the problems it finds, and each must contain every reason listed.
    private static readonly (string Name, Func<IReadOnlyList<string>> Walk, string[] Reasons)[] PlantedCases =
    [
        (
            "a date of birth as a date",
            () => Problems([typeof(PlantedDateOfBirth)], PlantedNamespace),
            [
                $"PlantedDateOfBirth.dateOfBirth: {OnTheDenyList}",
                $"PlantedDateOfBirth.dateOfBirth: System.DateOnly {NotALeaf}",
            ]),
        (
            "a narrative",
            () => Problems([typeof(PlantedNarrative)], PlantedNamespace),
            [$"PlantedNarrative.narrative: {OnTheDenyList}"]),
        (
            "a participant of the database",
            () => Problems([typeof(PlantedParticipantReference)], PlantedNamespace),
            [$"PlantedParticipantReference.participant: Grow2Notes.Web.Data.Participant {NotALeaf}"]),
        (
            "a comment in the items of a list",
            () => Problems([typeof(PlantedNestedComment)], PlantedNamespace),
            [$"PlantedNestedComment.items[].comment: {OnTheDenyList}"]),
        (
            "a count of flags",
            () => Problems([typeof(PlantedFlagCount)], PlantedNamespace),
            [$"PlantedFlagCount.toFlagCount: {OnTheDenyList}"]),
        (
            "the date of the first note",
            () => Problems([typeof(PlantedFirstNoteDate)], PlantedNamespace),
            [$"PlantedFirstNoteDate.firstNoteDate: {OnTheDenyList}"]),
        (
            "a JSON schema with a flag reason in the items of an array",
            () => SchemaProblems(JsonSerializer.Deserialize<JsonElement>(PlantedSchema)),
            [$"$.properties.versions.items.properties.flagReason: {OnTheDenyList}"]),
    ];

    public static TheoryData<string> PlantedViolations { get; } = [.. PlantedCases.Select(planted => planted.Name)];

    private static string PlantedNamespace => typeof(PlantedDateOfBirth).Namespace!;

    private static Assembly Web => typeof(ParticipantsTools).Assembly;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// The problems of <paramref name="roots"/>, walked through their properties, <see cref="Nullable{T}"/> and
    /// <see cref="IReadOnlyList{T}"/>: a name on the deny-list, and a type that is not a string, bool, int or Guid and
    /// not in <paramref name="allowedNamespace"/> (the contracts', for production). Each names the path of the
    /// property, with its name as JSON writes it. A task that adds a type that is not a tool's output, such as a
    /// confirmation form, adds it to the roots of its test.
    /// </summary>
    internal static IReadOnlyList<string> Problems(IEnumerable<Type> roots, string allowedNamespace)
    {
        var problems = new List<string>();
        foreach (var root in roots)
        {
            Walk(root, root.Name, [], problems, allowedNamespace);
        }

        return problems;
    }

    /// <summary>
    /// The problems of a JSON schema: a name on the deny-list at any depth, and a <c>$ref</c>, which is not followed.
    /// </summary>
    internal static IReadOnlyList<string> SchemaProblems(JsonElement schema)
    {
        var problems = new List<string>();
        WalkSchema(schema, "$", problems);
        return problems;
    }

    internal static bool IsDenied(string name) =>
        DeniedNames.Contains(name, StringComparer.Ordinal)
        || DeniedFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Every_tool_returns_a_structured_result_of_a_type_in_the_contracts()
    {
        var tools = ToolMethods();

        Assert.NotEmpty(tools);
        foreach (var (method, attribute) in tools)
        {
            Assert.True(method.ReturnType == typeof(Task<CallToolResult>), method.Name);
            Assert.True(attribute.UseStructuredContent, method.Name);
            Assert.Equal(ContractsNamespace, attribute.OutputSchemaType?.Namespace);
        }
    }

    [Fact]
    public void The_output_types_of_every_tool_pass_the_allow_list_and_the_deny_list()
    {
        var roots = ToolMethods().Select(tool => tool.Attribute.OutputSchemaType!).Distinct().ToList();

        Assert.NotEmpty(roots);
        Assert.Empty(Problems(roots, ContractsNamespace));
    }

    [Fact]
    public async Task The_tools_in_the_code_are_the_tools_production_lists()
    {
        await using var flow = await StartAsync();
        var scene = await ToolScene.CreateAsync(flow);
        await using var client = await flow.McpClientAsync(await scene.ConnectAsync(ToolCaller.Manager), Revision);

        var listed = await client.ListToolsAsync(cancellationToken: Ct);

        Assert.Equal(
            ToolMethods().Select(tool => tool.Attribute.Name).Order(StringComparer.Ordinal),
            listed.Select(tool => tool.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_output_schemas_production_lists_pass_the_deny_list()
    {
        await using var flow = await StartAsync();
        var scene = await ToolScene.CreateAsync(flow);
        await using var client = await flow.McpClientAsync(await scene.ConnectAsync(ToolCaller.Manager), Revision);

        var listed = await client.ListToolsAsync(cancellationToken: Ct);

        Assert.NotEmpty(listed);
        foreach (var tool in listed)
        {
            var schema = Assert.NotNull(tool.ProtocolTool.OutputSchema);
            Assert.Empty(SchemaProblems(schema));
        }
    }

    [Fact]
    public async Task What_each_tool_returns_is_in_its_output_schema_and_passes_the_deny_list()
    {
        await using var flow = await StartAsync();
        var scene = await ToolScene.CreateAsync(flow);
        await using var client = await flow.McpClientAsync(await scene.ConnectAsync(ToolCaller.Manager), Revision);
        var listed = (await client.ListToolsAsync(cancellationToken: Ct)).ToDictionary(tool => tool.Name);

        foreach (var row in ToolMatrix.Rows)
        {
            var result = await ToolScene.CallAsync(client, row.Name, await row.ValidArguments(scene));

            Assert.NotEqual(true, result.IsError);
            var content = Assert.NotNull(result.StructuredContent);
            var schema = Assert.NotNull(listed[row.Name].ProtocolTool.OutputSchema);
            Assert.Empty(ResultProblems(content, [schema], row.Name));
        }
    }

    [Theory]
    [MemberData(nameof(PlantedViolations))]
    public void The_rules_refuse_each_planted_violation_for_its_reason(string name)
    {
        var planted = PlantedCases.Single(candidate => candidate.Name == name);

        var problems = planted.Walk();

        Assert.All(planted.Reasons, reason => Assert.Contains(problems, problem => problem.Contains(reason)));
    }

    [Fact]
    public void The_rules_pass_the_contracts_and_a_schema_with_no_denied_name()
    {
        const string schema = """{"properties":{"participants":{"items":{"properties":{"participantId":{}}}}}}""";

        Assert.Empty(Problems([typeof(FoundParticipants), typeof(ParticipantWithGoals)], ContractsNamespace));
        Assert.Empty(SchemaProblems(JsonSerializer.Deserialize<JsonElement>(schema)));
    }

    private static List<(MethodInfo Method, McpServerToolAttribute Attribute)> ToolMethods() =>
    [
        .. Web.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(tool => tool.Attribute is not null)
            .Select(tool => (tool.Method, tool.Attribute!)),
    ];

    private static void Walk(Type type, string path, List<Type> walking, List<string> problems, string allowed)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(Guid))
        {
            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            Walk(type.GetGenericArguments()[0], path + "[]", walking, problems, allowed);
            return;
        }

        if (type.Namespace != allowed || type.IsEnum)
        {
            problems.Add($"{path}: {type.FullName} {NotALeaf}, and not in {allowed}");
            return;
        }

        if (walking.Contains(type))
        {
            return;
        }

        walking.Add(type);
        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (property.Name, property.PropertyType))
            .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(field => (field.Name, field.FieldType)));
        foreach (var (name, memberType) in members)
        {
            var json = JsonNamingPolicy.CamelCase.ConvertName(name);
            var memberPath = $"{path}.{json}";
            if (IsDenied(json))
            {
                problems.Add($"{memberPath}: {OnTheDenyList}");
            }

            Walk(memberType, memberPath, walking, problems, allowed);
        }

        walking.Remove(type);
    }

    // Everything under the schema is walked, whatever the keyword (properties, items, anyOf), and each name under a
    // "properties" is held to the deny-list.
    private static void WalkSchema(JsonElement node, string path, List<string> problems)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    WalkSchema(item, path, problems);
                }

                break;
            case JsonValueKind.Object:
                foreach (var member in node.EnumerateObject())
                {
                    if (member.Name == "$ref")
                    {
                        problems.Add($"{path}: $ref is not followed, so what it names is not checked");
                    }
                    else if (member.Name == "properties" && member.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in member.Value.EnumerateObject())
                        {
                            var propertyPath = $"{path}.properties.{property.Name}";
                            if (IsDenied(property.Name))
                            {
                                problems.Add($"{propertyPath}: {OnTheDenyList}");
                            }

                            WalkSchema(property.Value, propertyPath, problems);
                        }
                    }
                    else
                    {
                        WalkSchema(member.Value, $"{path}.{member.Name}", problems);
                    }
                }

                break;
        }
    }

    // A key of the result must be in the schema at the same path (each alternative of an anyOf counts) and pass the
    // deny-list; only a success is walked, because an error has its own shape.
    private static List<string> ResultProblems(JsonElement result, IReadOnlyList<JsonElement> schemas, string path)
    {
        var problems = new List<string>();
        var alternatives = schemas.SelectMany(Alternatives).ToList();
        switch (result.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var member in result.EnumerateObject())
                {
                    var memberPath = $"{path}.{member.Name}";
                    if (IsDenied(member.Name))
                    {
                        problems.Add($"{memberPath}: {OnTheDenyList}");
                    }

                    var declared = Inside(alternatives, "properties")
                        .Where(properties => properties.TryGetProperty(member.Name, out _))
                        .Select(properties => properties.GetProperty(member.Name))
                        .ToList();
                    if (declared.Count == 0)
                    {
                        problems.Add($"{memberPath}: the key is not in the output schema");
                    }

                    problems.AddRange(ResultProblems(member.Value, declared, memberPath));
                }

                break;
            case JsonValueKind.Array:
                var items = Inside(alternatives, "items").ToList();
                var elements = result.EnumerateArray().ToList();
                if (elements.Count > 0 && items.Count == 0)
                {
                    problems.Add($"{path}: the output schema has no items for the array");
                }

                foreach (var element in elements)
                {
                    problems.AddRange(ResultProblems(element, items, $"{path}[]"));
                }

                break;
        }

        return problems;
    }

    private static IEnumerable<JsonElement> Inside(IEnumerable<JsonElement> schemas, string keyword) =>
        schemas.Where(schema => schema.TryGetProperty(keyword, out _)).Select(schema => schema.GetProperty(keyword));

    // The schema itself and, at any depth, the alternatives of its anyOf, oneOf and allOf.
    private static IEnumerable<JsonElement> Alternatives(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        yield return schema;
        foreach (var keyword in Combinators)
        {
            if (schema.TryGetProperty(keyword, out var alternatives) && alternatives.ValueKind == JsonValueKind.Array)
            {
                foreach (var alternative in alternatives.EnumerateArray().SelectMany(Alternatives))
                {
                    yield return alternative;
                }
            }
        }
    }

    private Task<AssistantFlow> StartAsync() =>
        AssistantFlow.StartAsync(factory, sqlServer, "ContractRules", probes: false);
}
