using System.Text.Json;
using Aon.Content;
using Aon.Core;
using Aon.Rules;

namespace Aon.Tools.SeriesValidator;

public static class Program
{
    public static int Main(string[] args)
    {
        var fixturesRoot = ResolveFixturesRoot();
        var seriesFixtures = new[]
        {
            SeriesFixture.CreateLoneWolf(fixturesRoot),
            SeriesFixture.CreateGreyStar(fixturesRoot),
            SeriesFixture.CreateFreewayWarrior(fixturesRoot)
        };

        var failures = new List<string>();
        foreach (var fixture in seriesFixtures)
        {
            try
            {
                ValidateSeries(fixture);
                Console.WriteLine($"{fixture.SeriesId}: validation passed.");
            }
            catch (Exception ex)
            {
                failures.Add($"{fixture.SeriesId}: {ex.Message}");
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("All series validations completed successfully.");
            return 0;
        }

        Console.Error.WriteLine("Series validation failures:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }

        return 1;
    }

    private static void ValidateSeries(SeriesFixture fixture)
    {
        var book = LoadBook(fixture.BookPath);
        var ruleCatalogDocument = LoadRuleCatalogDocument(fixture.RuleCatalogPath);
        var catalog = RuleCatalog.Load(fixture.RuleCatalogPath);

        var metadataWarnings = RuleMetadataValidator.ValidateBook(book, catalog);
        if (metadataWarnings.Count > 0)
        {
            throw new InvalidOperationException($"Rule metadata validation failed: {string.Join(" ", metadataWarnings)}");
        }

        var tokenErrors = ValidateTokens(ruleCatalogDocument, book);
        if (tokenErrors.Count > 0)
        {
            throw new InvalidOperationException($"Rule token validation failed: {string.Join(" ", tokenErrors)}");
        }

        var engine = BuildRulesEngine(fixture);
        ValidateCombatResolution(engine, fixture);
        ValidateEffectApplication(engine, book, fixture);
    }

    private static RulesEngine BuildRulesEngine(SeriesFixture fixture)
    {
        return new RulesEngine(
            new RandomNumberTable(),
            new CombatResolver(),
            new DefaultRandomNumberGenerator(),
            new ConditionEvaluator(),
            seriesId =>
            {
                EnsureSeriesMatch(fixture.SeriesId, seriesId, "combat table");
                return CombatTable.Load(seriesId);
            },
            seriesId =>
            {
                EnsureSeriesMatch(fixture.SeriesId, seriesId, "rule catalog");
                return RuleCatalog.Load(fixture.RuleCatalogPath);
            });
    }

    private static void ValidateCombatResolution(RulesEngine engine, SeriesFixture fixture)
    {
        var player = new Character
        {
            CombatSkill = fixture.CombatScenario.PlayerCombatSkill,
            Endurance = fixture.CombatScenario.PlayerEndurance
        };

        var combatResult = engine.ResolveCombatRound(
            player,
            fixture.CombatScenario.EnemyCombatSkill,
            fixture.CombatScenario.EnemyEndurance,
            fixture.CombatScenario.RandomNumber,
            fixture.SeriesId);

        var tableOutcome = CombatTable.Load(fixture.SeriesId)
            .Resolve(player.CombatSkill - fixture.CombatScenario.EnemyCombatSkill, fixture.CombatScenario.RandomNumber);

        if (combatResult.PlayerLoss != tableOutcome.PlayerLoss || combatResult.EnemyLoss != tableOutcome.EnemyLoss)
        {
            throw new InvalidOperationException(
                $"Combat outcome mismatch. Expected {tableOutcome.PlayerLoss}/{tableOutcome.EnemyLoss} but got {combatResult.PlayerLoss}/{combatResult.EnemyLoss}.");
        }
    }

    private static void ValidateEffectApplication(RulesEngine engine, Book book, SeriesFixture fixture)
    {
        var section = book.Sections.First();
        var choice = section.Choices.First();
        var gameState = new GameState
        {
            SeriesId = fixture.SeriesId,
            BookId = book.Id,
            SectionId = section.Id,
            Character = fixture.EffectScenario.SeedCharacter,
            Flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        var context = new RuleContext(gameState);
        var ruleSet = engine.ResolveChoiceRules(choice, context);
        engine.ApplyEffects(ruleSet.Effects, context);

        fixture.EffectScenario.Assert(gameState);
    }

    private static List<string> ValidateTokens(RuleCatalogDocument ruleCatalog, Book book)
    {
        var errors = new List<string>();

        foreach (var rule in ruleCatalog.Rules)
        {
            errors.AddRange(ValidateRequirements(rule.Requirements, $"rule {rule.Id}"));
            errors.AddRange(ValidateEffects(rule.Effects, $"rule {rule.Id}"));
        }

        foreach (var section in book.Sections)
        {
            foreach (var choice in section.Choices)
            {
                errors.AddRange(ValidateRequirements(choice.Requirements, $"section {section.Id} choice"));
                errors.AddRange(ValidateEffects(choice.Effects, $"section {section.Id} choice"));

                foreach (var outcome in choice.RandomOutcomes)
                {
                    errors.AddRange(ValidateEffects(outcome.Effects, $"section {section.Id} random outcome"));
                }
            }
        }

        return errors;
    }

    private static IEnumerable<string> ValidateRequirements(IEnumerable<string> requirements, string context)
    {
        foreach (var requirement in requirements)
        {
            if (RequirementParser.Parse(requirement) is UnsupportedRequirement)
            {
                yield return $"Unsupported requirement '{requirement}' in {context}.";
            }
        }
    }

    private static IEnumerable<string> ValidateEffects(IEnumerable<string> effects, string context)
    {
        foreach (var effect in effects)
        {
            if (EffectParser.Parse(effect) is UnsupportedEffect)
            {
                yield return $"Unsupported effect '{effect}' in {context}.";
            }
        }
    }

    private static Book LoadBook(string path)
    {
        var json = File.ReadAllText(path);
        var book = JsonSerializer.Deserialize<Book>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (book is null)
        {
            throw new InvalidOperationException($"Unable to deserialize book fixture at {path}.");
        }

        return book;
    }

    private static RuleCatalogDocument LoadRuleCatalogDocument(string path)
    {
        using var stream = File.OpenRead(path);
        var document = JsonSerializer.Deserialize(
            stream,
            RuleCatalogDocumentContext.Default.RuleCatalogDocument);

        if (document is null)
        {
            throw new InvalidOperationException($"Unable to deserialize rule catalog fixture at {path}.");
        }

        return document;
    }

    private static string ResolveFixturesRoot()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        if (!Directory.Exists(candidate))
        {
            throw new DirectoryNotFoundException($"Fixtures directory not found at {candidate}.");
        }

        return candidate;
    }

    private static void EnsureSeriesMatch(string expected, string? actual, string assetType)
    {
        if (!string.Equals(expected, actual ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Expected {assetType} for series '{expected}', but received '{actual}'.");
        }
    }
}

internal sealed record SeriesFixture(
    string SeriesId,
    string BookPath,
    string RuleCatalogPath,
    CombatScenario CombatScenario,
    EffectScenario EffectScenario)
{
    public static SeriesFixture CreateLoneWolf(string root)
    {
        var seriesRoot = Path.Combine(root, "LoneWolf");
        return new SeriesFixture(
            "LoneWolf",
            Path.Combine(seriesRoot, "Book.LoneWolf.Sample.json"),
            Path.Combine(seriesRoot, "RulesCatalog.LoneWolf.Sample.json"),
            new CombatScenario(10, 20, 30, 20, 0),
            EffectScenario.LoneWolf());
    }

    public static SeriesFixture CreateGreyStar(string root)
    {
        var seriesRoot = Path.Combine(root, "GreyStar");
        return new SeriesFixture(
            "GreyStar",
            Path.Combine(seriesRoot, "Book.GreyStar.Sample.json"),
            Path.Combine(seriesRoot, "RulesCatalog.GreyStar.Sample.json"),
            new CombatScenario(11, 15, 28, 16, 3),
            EffectScenario.GreyStar());
    }

    public static SeriesFixture CreateFreewayWarrior(string root)
    {
        var seriesRoot = Path.Combine(root, "FreewayWarrior");
        return new SeriesFixture(
            "FreewayWarrior",
            Path.Combine(seriesRoot, "Book.FreewayWarrior.Sample.json"),
            Path.Combine(seriesRoot, "RulesCatalog.FreewayWarrior.Sample.json"),
            new CombatScenario(9, 12, 23, 14, 5),
            EffectScenario.FreewayWarrior());
    }
}

internal sealed record CombatScenario(
    int PlayerCombatSkill,
    int PlayerEndurance,
    int EnemyCombatSkill,
    int EnemyEndurance,
    int RandomNumber);

internal sealed record EffectScenario(Character SeedCharacter, Action<GameState> Assert)
{
    public static EffectScenario LoneWolf()
    {
        var character = new Character
        {
            CombatSkill = 10,
            Endurance = 20
        };

        void Assert(GameState state)
        {
            if (state.Character.CombatSkill != 11)
            {
                throw new InvalidOperationException("Expected Lone Wolf combat skill to increase to 11.");
            }

            if (state.Character.Endurance != 22)
            {
                throw new InvalidOperationException("Expected Lone Wolf endurance to increase to 22.");
            }

            state.Character.Attributes.TryGetValue(Character.CombatSkillBonusAttribute, out var bonus);
            if (bonus != 2)
            {
                throw new InvalidOperationException("Expected Lone Wolf combat bonus to be 2.");
            }

            if (!state.Flags.TryGetValue("mindblast", out var flagValue) || flagValue != "true")
            {
                throw new InvalidOperationException("Expected Lone Wolf mindblast flag to be true.");
            }

            if (!state.Character.Inventory.Counters.TryGetValue("herb", out var herbCount) || herbCount != 0)
            {
                throw new InvalidOperationException("Expected Lone Wolf herb counter to resolve to 0.");
            }

            if (!state.Character.Inventory.Items.Any(item => item.Name == "Laumspur"))
            {
                throw new InvalidOperationException("Expected Lone Wolf inventory to include Laumspur.");
            }
        }

        return new EffectScenario(character, Assert);
    }

    public static EffectScenario GreyStar()
    {
        var character = new Character
        {
            CombatSkill = 11,
            Endurance = 15,
            Attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Willpower"] = 4
            }
        };

        void Assert(GameState state)
        {
            if (state.Character.Endurance != 15)
            {
                throw new InvalidOperationException("Expected Grey Star endurance to resolve to 15.");
            }

            if (!state.Character.Attributes.TryGetValue("Willpower", out var willpower) || willpower != 5)
            {
                throw new InvalidOperationException("Expected Grey Star willpower to be 5.");
            }

            if (!state.Flags.TryGetValue("spell", out var spellFlag) || spellFlag != "ready")
            {
                throw new InvalidOperationException("Expected Grey Star spell flag to be ready.");
            }

            if (!state.Character.Inventory.Counters.TryGetValue("mana", out var mana) || mana != 1)
            {
                throw new InvalidOperationException("Expected Grey Star mana counter to resolve to 1.");
            }

            if (!state.Character.Inventory.Counters.TryGetValue("slot:backpack", out var slots) || slots != 1)
            {
                throw new InvalidOperationException("Expected Grey Star backpack slot to be 1.");
            }

            if (!state.Character.Inventory.Items.Any(item => item.Name == "Crystal"))
            {
                throw new InvalidOperationException("Expected Grey Star inventory to include Crystal.");
            }
        }

        return new EffectScenario(character, Assert);
    }

    public static EffectScenario FreewayWarrior()
    {
        var character = new Character
        {
            CombatSkill = 9,
            Endurance = 12
        };

        void Assert(GameState state)
        {
            if (state.Character.Endurance != 14)
            {
                throw new InvalidOperationException("Expected Freeway Warrior endurance to resolve to 14.");
            }

            state.Character.Attributes.TryGetValue(Character.CombatSkillBonusAttribute, out var bonus);
            if (bonus != 1)
            {
                throw new InvalidOperationException("Expected Freeway Warrior combat bonus to be 1.");
            }

            if (!state.Character.Disciplines.Contains("Gunplay", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected Freeway Warrior to gain Gunplay discipline.");
            }

            if (!state.Flags.TryGetValue("highway", out var highway) || highway != "cleared")
            {
                throw new InvalidOperationException("Expected Freeway Warrior highway flag to be cleared.");
            }

            if (!state.Character.Inventory.Counters.TryGetValue("ammo", out var ammo) || ammo != 3)
            {
                throw new InvalidOperationException("Expected Freeway Warrior ammo counter to resolve to 3.");
            }

            if (state.Character.Inventory.Items.Any(item => item.Name == "Fuel"))
            {
                throw new InvalidOperationException("Expected Freeway Warrior fuel to be removed.");
            }
        }

        return new EffectScenario(character, Assert);
    }
}
