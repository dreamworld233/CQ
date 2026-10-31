using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

internal static class AutoBattleTestData
{
    public static SkillEffectSpec Effect(string type = "damage", string target = "enemyFront",
        int count = 1, int multiplier = 100, int hits = 1, int percent = 0, int duration = 0, int amount = 0) =>
        new SkillEffectSpec
        {
            type = type, target = target, count = count, multiplierPercent = multiplier,
            hits = hits, percent = percent, duration = duration, amount = amount
        };

    public static SkillSpec Skill(string type, params SkillEffectSpec[] effects) => new SkillSpec
    {
        id = type, name = type, type = type, trigger = type == "passive" ? "roundStart" : null,
        effects = effects.ToList()
    };

    public static CharacterConfig Hero(string id = "hero", string slot = "front1", int attack = 10,
        int defense = 0, int speed = 10, int hp = 10000) => new CharacterConfig
    {
        id = id, name = id, defaultSlot = slot, maxHp = hp,
        baseAttack = attack, baseDefense = defense, baseSpeed = speed,
        initialEnergy = 20, maxEnergy = 100, actionEnergy = 40, hitEnergy = 10, specialInterval = 2,
        skills = new List<SkillSpec>
        {
            Skill("basic", Effect()), Skill("special", Effect(multiplier: 200)),
            Skill("ult", Effect(multiplier: 400)),
            Skill("passive", Effect("defenseUp", "self", multiplier: 0, hits: 0, percent: 1, duration: 1))
        }
    };

    public static EnemyConfig Enemy(string id = "enemy", string slot = "front1", int attack = 1,
        int defense = 0, int speed = 1, int hp = 10000)
    {
        var source = Hero(id, slot, attack, defense, speed, hp);
        return new EnemyConfig
        {
            id = source.id, name = source.name, defaultSlot = source.defaultSlot,
            maxHp = source.maxHp, baseAttack = source.baseAttack, baseDefense = source.baseDefense,
            baseSpeed = source.baseSpeed, initialEnergy = source.initialEnergy,
            maxEnergy = source.maxEnergy, actionEnergy = source.actionEnergy,
            hitEnergy = source.hitEnergy, specialInterval = source.specialInterval, skills = source.skills
        };
    }

    public static void SetSkill(CharacterConfig config, SkillSpec skill)
    {
        config.skills[config.skills.FindIndex(s => s.type == skill.type)] = skill;
    }

    public static BattleEngine Engine(long seed = 7, CharacterConfig[] heroes = null,
        EnemyConfig[] enemies = null, string[] enemySlots = null)
    {
        heroes = heroes ?? new[] { Hero() };
        enemies = enemies ?? new[] { Enemy() };
        var wave = new WaveConfig
        {
            enemies = enemies.Select(e => e.id).ToArray(),
            slots = enemySlots ?? enemies.Select(e => e.defaultSlot).ToArray()
        };
        return BattleEngine.Create(seed, heroes, wave, enemies.GroupBy(e => e.id).ToDictionary(g => g.Key, g => g.First()));
    }
}
