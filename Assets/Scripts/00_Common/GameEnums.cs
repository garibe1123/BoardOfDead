using System;

namespace BoardOfDead
{
    public enum GamePhase
    {
        None,
        Setup,
        RadioBroadcast,
        PlayerTurnQueue,
        Environment,
        Doom,
        EndCheck,
        GameOver
    }

    public enum PlayerTurnPhase
    {
        None,
        TurnStart,
        MoveSelect,
        Moving,
        NodeEnter,
        CardReveal,
        SanCheck,
        ActionSelect,
        ResolvingAction,
        TurnEnd
    }

    public enum PlayerLifeState
    {
        Survivor,
        Infected,
        Incapacitated,
        Escaped,
        Dead
    }

    public enum BoardSpaceType
    {
        Empty,
        Road,
        Building
    }

    public enum BuildingType
    {
        Generic,
        Residential,
        Commercial,
        Industrial,
        Hospital,
        PoliceStation,
        FireStation,
        GasStation,
        Warehouse,
        Market,
        PublicFacility,
        Other
    }

    public enum CardType
    {
        Zombie,
        Crisis,
        Supply,
        Story,
        EscapeRoute,
        Vehicle
    }

    public enum DistrictType
    {
        Residential,
        Commercial,
        Industrial,
        Medical,
        Government,
        Mixed
    }

    public enum BoardNodeType
    {
        ResidentialBuilding,
        CommercialBuilding,
        IndustrialBuilding,
        Hospital,
        PoliceStation,
        FireStation,
        GasStation,
        Warehouse,
        Market,
        PublicFacility,
        ParkingArea,
        Other
    }

    public enum BoardConnectionType
    {
        NormalRoad,
        ElevatedRoad
    }

    public enum ItemType
    {
        General,
        Consumable,
        Weapon,
        Ammo,
        RepairPart,
        Fuel,
        EscapeMaterial,
        Quest
    }

    public enum VehicleType
    {
        Sedan,
        Van,
        PickupTruck,
        Motorcycle,
        UtilityVehicle,
        Other
    }

    public enum EscapeRouteType
    {
        Helicopter,
        ArmoredVehicle,
        LightAircraft,
        Sewer
    }

    public enum LogCategory
    {
        System,
        Round,
        Radio,
        Turn,
        Movement,
        Search,
        Encounter,
        Card,
        Vehicle,
        Escape,
        Environment,
        Doom,
        Sanity
    }

    public enum DistrictDirection
    {
        North,
        East,
        South,
        West
    }

    public enum DistrictLinkDirection
    {
        East,
        North
    }

    [Flags]
    public enum RoadConnectionMask
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }
}
