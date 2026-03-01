using System;
using System.Collections.Generic;
using System.Linq;

// ИНТЕРФЕЙСЫ 

public interface IUnit
{
    string Name { get; }
    int Health { get; }
    int MaxHealth { get; }
    int Damage { get; }
    int Armor { get; }
    int Cost { get; } 
    bool IsAlive { get; }
    void Attack(IUnit target);
    void TakeDamage(int damage);
}

public interface IRangedUnit : IUnit
{
    int Range { get; }

    bool CanShoot(int unitsBetween);
}

public interface IArmy
{
    string Name { get; }
    int TotalCost { get; }
    IReadOnlyList<IUnit> Units { get; }
    bool IsAlive { get; }
    IUnit GetFirstLiving();
    void AddUnit(IUnit unit);
    void Clear();
}

public interface IUnitFactory
{
    IUnit CreateUnit(string unitType);
    IEnumerable<string> GetAvailableUnitTypes();
    int GetUnitCost(string unitType);
}

public interface IRandomArmyGenerator
{
    IArmy GenerateRandomArmy(string name, int goldAmount);
}

public interface IRangedCombatService
{
    void ExecuteRangedCombat(IRangedUnit attacker, IUnit defender, int unitsBetween, ICombatLogger logger);
}

public interface IArcherPhaseService
{
    void ExecuteArcherPhase(IArmy army1, IArmy army2, ICombatLogger logger);
}

public interface IMeleePhaseService
{
    void ExecuteMeleePhase(IArmy army1, IArmy army2, ICombatLogger logger);
}

public interface ICombatLogger
{
    void Log(string message);
    void LogUnitDeath(IUnit unit);
    void LogDamage(IUnit target, int damage, int actualDamage, int armor);
    void Clear();
    string GetFullLog();
}

public interface IGameUI
{
    void ShowMainMenu();
    void ShowBattleStatus(IArmy army1, IArmy army2, int round);
    void ShowBattleResult(IArmy army1, IArmy army2);
    void ShowInstructions();
    void ShowArmyCreationMenu(int availableGold);
    string GetUserInput();
    void WaitForUser();
    void ClearScreen();
}

public interface IGameController
{
    void Run();
}

public interface IBattle
{
    void Start(IArmy army1, IArmy army2);
}

// РЕАЛИЗАЦИЯ ЮНИТОВ

public abstract class Unit : IUnit
{
    public string Name { get; protected set; }
    public int Health { get; protected set; }
    public int MaxHealth { get; protected set; }
    public int Damage { get; protected set; }
    public int Armor { get; protected set; }
    public int Cost { get; protected set; }
    public bool IsAlive => Health > 0;

    protected Unit(string name, int health, int damage, int armor, int cost)
    {
        Name = name;
        Health = health;
        MaxHealth = health;
        Damage = damage;
        Armor = armor;
        Cost = cost;
    }

    public virtual void Attack(IUnit target)
    {
        target.TakeDamage(Damage);
    }

    public virtual void TakeDamage(int damage)
    {
        int reducedDamage = Math.Max(1, damage - Armor);
        Health = Math.Max(0, Health - reducedDamage);
    }
}

public class Bruiser : Unit
{
    public Bruiser() : base("Bruiser", 200, 30, 2, 100)
    {
    }
}

public class Archer : Unit, IRangedUnit
{
    public int Range { get; private set; }

    public Archer() : base("Archer", 100, 40, 0, 80)
    {
        Range = 4;
    }

    public bool CanShoot(int unitsBetween)
    {
        return unitsBetween <= Range;
    }
}

public class Tank : Unit
{
    public Tank() : base("Tank", 350, 15, 10, 150)
    {
    }
}

// ФАБРИКА И АРМИЯ

public class UnitFactory : IUnitFactory
{
    private readonly Dictionary<string, Func<IUnit>> _unitCreators = new Dictionary<string, Func<IUnit>>
    {
        ["Bruiser"] = () => new Bruiser(),
        ["Archer"] = () => new Archer(),
        ["Tank"] = () => new Tank()
    };

    public IUnit CreateUnit(string unitType)
    {
        if (_unitCreators.TryGetValue(unitType, out var creator))
            return creator();

        throw new ArgumentException($"Unknown unit type: {unitType}");
    }

    public IEnumerable<string> GetAvailableUnitTypes()
    {
        return _unitCreators.Keys;
    }

    public int GetUnitCost(string unitType)
    {
        if (_unitCreators.TryGetValue(unitType, out var creator))
        {
            var tempUnit = creator();
            // ИСПРАВЛЕНИЕ LSP: Больше нет приведения к (Unit). 
            // Теперь мы берем Cost напрямую через интерфейс IUnit.
            return tempUnit.Cost; 
        }
        return 0;
    }
}

public class Army : IArmy
{
    private readonly List<IUnit> _units = new List<IUnit>();

    public string Name { get; private set; }
    public int TotalCost { get; private set; }
    public IReadOnlyList<IUnit> Units => _units.AsReadOnly();
    public bool IsAlive => _units.Any(u => u.IsAlive);

    public Army(string name)
    {
        Name = name;
    }

    public IUnit GetFirstLiving()
    {
        return _units.FirstOrDefault(u => u.IsAlive);
    }

    public void AddUnit(IUnit unit)
    {
        _units.Add(unit);
        // ИСПРАВЛЕНИЕ LSP: Теперь мы не кастим к ((Unit)unit).
        // Мы доверяем интерфейсу IUnit, что у любого юнита есть свойство Cost.
        TotalCost += unit.Cost; 
    }

    public void Clear()
    {
        _units.Clear();
        TotalCost = 0;
    }
}

// СЕРВИСЫ БОЯ

public class RandomArmyGenerator : IRandomArmyGenerator
{
    private readonly IUnitFactory _unitFactory;
    private readonly Random _random = new Random();

    public RandomArmyGenerator(IUnitFactory unitFactory)
    {
        _unitFactory = unitFactory;
    }

    public IArmy GenerateRandomArmy(string name, int goldAmount)
    {
        var army = new Army(name);
        int remainingGold = goldAmount;
        var unitTypes = _unitFactory.GetAvailableUnitTypes().ToList();

        while (remainingGold > 0)
        {
            string randomUnitType = unitTypes[_random.Next(unitTypes.Count)];
            int unitCost = _unitFactory.GetUnitCost(randomUnitType);

            if (unitCost <= remainingGold)
            {
                var unit = _unitFactory.CreateUnit(randomUnitType);
                army.AddUnit(unit);
                remainingGold -= unitCost;
            }
            else
            {
                bool canAffordAny = unitTypes.Any(t => _unitFactory.GetUnitCost(t) <= remainingGold);
                if (!canAffordAny)
                    break;
            }
        }

        return army;
    }
}

public class RangedCombatService : IRangedCombatService
{
    public void ExecuteRangedCombat(IRangedUnit attacker, IUnit defender, int unitsBetween, ICombatLogger logger)
    {
        if (!attacker.CanShoot(unitsBetween))
            return;

        int healthBefore = defender.Health;
        attacker.Attack(defender);
        int actualDamage = healthBefore - defender.Health;

        logger.Log($" {attacker.Name} стреляет в {defender.Name}! (через {unitsBetween} юнитов)");
        logger.LogDamage(defender, attacker.Damage, actualDamage, defender.Armor);
    }
}

public class ArcherPhaseService : IArcherPhaseService
{
    private readonly IRangedCombatService _rangedCombat;

    public ArcherPhaseService(IRangedCombatService rangedCombat)
    {
        _rangedCombat = rangedCombat;
    }

    public void ExecuteArcherPhase(IArmy army1, IArmy army2, ICombatLogger logger)
    {
        logger.Log("ФАЗА СТРЕЛЬБЫ");

        bool anyShot = false;

        anyShot |= ShootArchers(army1, army2, logger);
        anyShot |= ShootArchers(army2, army1, logger);

        if (!anyShot)
        {
            logger.Log("Никто не стрелял в этом раунде");
        }

        logger.Log("");
    }

    private bool ShootArchers(IArmy shootingArmy, IArmy enemyArmy, ICombatLogger logger)
    {
        bool anyShot = false;
        var livingUnits = shootingArmy.Units.Where(u => u.IsAlive).ToList();

        foreach (var unit in livingUnits)
        {
            if (unit is not IRangedUnit archer) continue;

            int archerIndex = livingUnits.IndexOf(unit);
            int unitsBetween = archerIndex;

            var target = enemyArmy.GetFirstLiving();
            if (target == null) break;

            if (archer.CanShoot(unitsBetween))
            {
                _rangedCombat.ExecuteRangedCombat(archer, target, unitsBetween, logger);
                anyShot = true;
            }
        }

        return anyShot;
    }
}

public class MeleePhaseService : IMeleePhaseService
{
    public void ExecuteMeleePhase(IArmy army1, IArmy army2, ICombatLogger logger)
    {
        logger.Log(" ФАЗА БЛИЖНЕГО БОЯ ");

        var unit1 = army1.GetFirstLiving();
        var unit2 = army2.GetFirstLiving();

        if (unit1 == null || unit2 == null)
            return;

        logger.Log($" {unit1.Name} атакует {unit2.Name}!");
        unit1.Attack(unit2);

        if (unit2.IsAlive)
        {
            logger.Log($" {unit2.Name} отвечает {unit1.Name}!");
            unit2.Attack(unit1);
        }

        logger.Log("");
    }
}

// ЛОГИРОВАНИЕ И БИТВА

public class CombatLogger : ICombatLogger
{
    private readonly List<string> _log = new List<string>();

    public void Log(string message)
    {
        _log.Add(message);
        Console.WriteLine(message);
    }

    public void LogUnitDeath(IUnit unit)
    {
        string message = $"  {unit.Name} погиб!";
        _log.Add(message);
        Console.WriteLine(message);
    }

    public void LogDamage(IUnit target, int damage, int actualDamage, int armor)
    {
        string armorInfo = armor > 0 && damage > actualDamage
            ? $" (броня {armor} поглотила {damage - actualDamage})"
            : "";

        string message = $"  Нанесено урона: {actualDamage}{armorInfo}";
        _log.Add(message);
        Console.WriteLine(message);

        if (!target.IsAlive)
        {
            LogUnitDeath(target);
        }
    }

    public void Clear()
    {
        _log.Clear();
    }

    public string GetFullLog()
    {
        return string.Join("\n", _log);
    }
}

public class Battle : IBattle
{
    private readonly IMeleePhaseService _meleePhase;
    private readonly IArcherPhaseService _archerPhase;
    private readonly ICombatLogger _logger;
    private readonly IGameUI _ui;

    public Battle(
        IMeleePhaseService meleePhase,
        IArcherPhaseService archerPhase,
        ICombatLogger logger,
        IGameUI ui)
    {
        _meleePhase = meleePhase;
        _archerPhase = archerPhase;
        _logger = logger;
        _ui = ui;
    }

    public void Start(IArmy army1, IArmy army2)
    {
        _ui.ClearScreen();
        _logger.Log(" ЗА ЧАПКИНА \n");
        _logger.Log("БИТВА НАЧИНАЕТСЯ!\n");

        int round = 1;

        while (army1.IsAlive && army2.IsAlive)
        {
            _ui.ShowBattleStatus(army1, army2, round);

            _meleePhase.ExecuteMeleePhase(army1, army2, _logger);

            if (!army1.IsAlive || !army2.IsAlive)
                break;

            _archerPhase.ExecuteArcherPhase(army1, army2, _logger);

            _ui.WaitForUser();
            round++;
            _ui.ClearScreen();
        }

        _ui.ShowBattleResult(army1, army2);
        _ui.WaitForUser();
    }
}

// ИНТЕРФЕЙС ПОЛЬЗОВАТЕЛЯ

public class ConsoleUI : IGameUI
{
    public void ShowMainMenu()
    {
        Console.WriteLine("╔════════════════════════════╗");
        Console.WriteLine("║        ЗА ЧАПКИНА!         ║");
        Console.WriteLine("╠════════════════════════════╣");
        Console.WriteLine("║ 1. Новая битва             ║");
        Console.WriteLine("║ 2. Инструкция              ║");
        Console.WriteLine("║ 3. Выход                   ║");
        Console.WriteLine("╚════════════════════════════╝");
        Console.Write("\nВаш выбор: ");
    }

    public void ShowBattleStatus(IArmy army1, IArmy army2, int round)
    {
        Console.WriteLine($"РАУНД {round}");
        Console.WriteLine("═══════════════════════════");

        Console.WriteLine($"{army2.Name}:");
        var enemyLiving = army2.Units.Where(u => u.IsAlive).ToList();
        for (int i = 0; i < enemyLiving.Count; i++)
        {
            string armorInfo = enemyLiving[i].Armor > 0 ? $" [Броня {enemyLiving[i].Armor}]" : "";
            Console.WriteLine($" {enemyLiving[i].Name} [{enemyLiving[i].Health}/{enemyLiving[i].MaxHealth}]{armorInfo}");
        }

        Console.WriteLine($"\n{army1.Name}:");
        var playerLiving = army1.Units.Where(u => u.IsAlive).ToList();
        for (int i = 0; i < playerLiving.Count; i++)
        {
            string armorInfo = playerLiving[i].Armor > 0 ? $" [Броня {playerLiving[i].Armor}]" : "";
            Console.WriteLine($" {playerLiving[i].Name} [{playerLiving[i].Health}/{playerLiving[i].MaxHealth}]{armorInfo}");
        }

        Console.WriteLine("\n═══════════════════════════");
    }

    public void ShowBattleResult(IArmy army1, IArmy army2)
    {
        ClearScreen();
        Console.WriteLine(" БИТВА ОКОНЧЕНА \n");
        Console.WriteLine("ИТОГОВАЯ СТАТИСТИКА:\n");

        Console.WriteLine($"{army2.Name}:");
        foreach (var unit in army2.Units)
        {
            string status = unit.IsAlive ? $" {unit.Health}" : " Погиб";
            Console.WriteLine($" {unit.Name}: {status}");
        }

        Console.WriteLine($"\n{army1.Name}:");
        foreach (var unit in army1.Units)
        {
            string status = unit.IsAlive ? $" {unit.Health}" : " Погиб";
            Console.WriteLine($"  {unit.Name}: {status}");
        }

        Console.WriteLine("\n═══════════════════════════\n");

        if (army1.IsAlive)
            Console.WriteLine(" ПОБЕДА! СЛАВА ЧАПКИНУ! ");
        else if (army2.IsAlive)
            Console.WriteLine(" ПОРАЖЕНИЕ... ЧАПКИН БЫЛ БЫ РАЗОЧАРОВАН ");
        else
            Console.WriteLine(" НИЧЬЯ! ВСЕ ПОГИБЛИ, НО ЧАПКИН ГОРД! ");
    }

    public void ShowInstructions()
    {
        ClearScreen();
        Console.WriteLine(" ИНСТРУКЦИЯ \n");
        Console.WriteLine("ЗА ЧАПКИНА!\n");
        Console.WriteLine("Типы юнитов:");
        Console.WriteLine(" Bruiser - 100 золота: 200 HP, 30 DMG, Броня 2 - воин");
        Console.WriteLine(" Archer - 80 золота: 100 HP, 40 DMG, Броня 0, дальность 4 - лучник");
        Console.WriteLine(" Tank - 150 золота: 350 HP, 15 DMG, Броня 10 - танк\n");
        Console.WriteLine("Правила битвы:");
        Console.WriteLine("1. Юниты выстроены в колонну");
        Console.WriteLine("2. Каждый раунд состоит из двух фаз:\n");
        Console.WriteLine("   ФАЗА 1 - Ближний бой (Первые атакуют друг друга)");
        Console.WriteLine("   ФАЗА 2 - Стрельба (Лучники бьют по дистанции)\n");
        Console.WriteLine("Цель: уничтожить вражескую армию!");
        Console.WriteLine("СЛАВА ЧАПКИНУ!\n");
        WaitForUser();
    }

    public void ShowArmyCreationMenu(int availableGold)
    {
        Console.WriteLine("\nДоступные юниты:");
        Console.WriteLine("1. Bruiser - 100 золота");
        Console.WriteLine("2. Archer - 80 золота");
        Console.WriteLine("3. Tank - 150 золота");
        Console.WriteLine("4. Закончить создание армии");
        Console.Write("Выберите юнита: ");
    }

    public string GetUserInput() => Console.ReadLine();
    public void WaitForUser()
    {
        Console.WriteLine("\nНажмите Enter для продолжения...");
        Console.ReadLine();
    }
    public void ClearScreen() => Console.Clear();
}

// КОНТРОЛЛЕР И ТОЧКА ВХОДА

public class GameController : IGameController
{
    private readonly IGameUI _ui;
    private readonly IUnitFactory _unitFactory;
    private readonly IRandomArmyGenerator _randomArmyGenerator;
    private readonly ICombatLogger _logger;
    private readonly IRangedCombatService _rangedCombat;

    public GameController(
        IGameUI ui,
        IUnitFactory unitFactory,
        IRandomArmyGenerator randomArmyGenerator,
        ICombatLogger logger,
        IRangedCombatService rangedCombat)
    {
        _ui = ui;
        _unitFactory = unitFactory;
        _randomArmyGenerator = randomArmyGenerator;
        _logger = logger;
        _rangedCombat = rangedCombat;
    }

    public void Run()
    {
        while (true)
        {
            _ui.ClearScreen();
            _ui.ShowMainMenu();

            string choice = _ui.GetUserInput();

            switch (choice)
            {
                case "1":
                    StartNewGame();
                    break;
                case "2":
                    _ui.ShowInstructions();
                    break;
                case "3":
                    Console.WriteLine("\nСпасибо за игру! За Чапкина!");
                    return;
                default:
                    Console.WriteLine("\nНеверный выбор!");
                    _ui.WaitForUser();
                    break;
            }
        }
    }

    private void StartNewGame()
    {
        _ui.ClearScreen();
        Console.WriteLine(" НОВАЯ БИТВА \n");

        IArmy playerArmy = CreatePlayerArmy();

        Console.Write("Введите количество золота врага: ");
        int.TryParse(Console.ReadLine(), out int enemyGold);
        IArmy enemyArmy = _randomArmyGenerator.GenerateRandomArmy("Вражеская армия", enemyGold);

        _ui.ClearScreen();
        Console.WriteLine(" АРМИИ СОЗДАНЫ \n");
        Console.WriteLine(GetArmyInfo(playerArmy));
        Console.WriteLine();
        Console.WriteLine(GetArmyInfo(enemyArmy));

        _ui.WaitForUser();

        var meleePhase = new MeleePhaseService();
        var archerPhase = new ArcherPhaseService(_rangedCombat);
        var battle = new Battle(meleePhase, archerPhase, _logger, _ui);

        battle.Start(playerArmy, enemyArmy);
    }

    private IArmy CreatePlayerArmy()
    {
        Console.WriteLine("Выберите способ создания армии:");
        Console.WriteLine("1. Случайная армия");
        Console.WriteLine("2. Ручное создание");
        Console.Write("Ваш выбор: ");

        string choice = _ui.GetUserInput();

        if (choice == "1")
        {
            Console.Write("Введите бюджет: ");
            int.TryParse(Console.ReadLine(), out int gold);
            return _randomArmyGenerator.GenerateRandomArmy("Ваша армия", gold);
        }

        return ManualArmyCreation();
    }

    private IArmy ManualArmyCreation()
    {
        _ui.ClearScreen();
        Console.WriteLine(" РУЧНОЕ СОЗДАНИЕ АРМИИ \n");

        var army = new Army("Ваша армия");
        int availableGold = 500;

        while (availableGold >= 80)
        {
            _ui.ShowArmyCreationMenu(availableGold);
            string choice = _ui.GetUserInput();

            if (choice == "1" && availableGold >= 100)
            {
                army.AddUnit(_unitFactory.CreateUnit("Bruiser"));
                availableGold -= 100;
            }
            else if (choice == "2" && availableGold >= 80)
            {
                army.AddUnit(_unitFactory.CreateUnit("Archer"));
                availableGold -= 80;
            }
            else if (choice == "3" && availableGold >= 150)
            {
                army.AddUnit(_unitFactory.CreateUnit("Tank"));
                availableGold -= 150;
            }
            else if (choice == "4") break;

            Console.WriteLine($"Осталось золота: {availableGold}");
        }

        return army;
    }

    private string GetArmyInfo(IArmy army)
    {
        string info = $"{army.Name} (Золото: {army.TotalCost}):\n";
        for (int i = 0; i < army.Units.Count; i++)
        {
            var unit = army.Units[i];
            info += $"  {i + 1}. {unit.Name}: HP={unit.Health}/{unit.MaxHealth}, DMG={unit.Damage}\n";
        }
        return info;
    }
}

public class CompositionRoot
{
    public static IGameController CreateGame()
    {
        var logger = new CombatLogger();
        var ui = new ConsoleUI();
        var factory = new UnitFactory();
        return new GameController(ui, factory, new RandomArmyGenerator(factory), logger, new RangedCombatService());
    }
}

class Program
{
    static void Main()
    {
        CompositionRoot.CreateGame().Run();
    }
}