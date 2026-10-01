using System.Collections.Generic;
using System.Linq;

namespace MalumMenu;

public static class DoorsHandler
{
    // Retourne une liste de toutes les salles qui ont des portes
    public static List<SystemTypes> GetRoomsWithDoors()
    {
        if (!Utils.isShip || ShipStatus.Instance.AllDoors.Count <= 0) return new List<SystemTypes>();

        return ShipStatus.Instance.AllDoors.Select(d => d.Room).Distinct().ToList();
    }

    // Retourne une liste de toutes les portes dans une salle spécifiée
    public static List<OpenableDoor> GetDoorsInRoom(SystemTypes room)
    {
        if (!Utils.isShip || ShipStatus.Instance.AllDoors.Count <= 0) return new List<OpenableDoor>();

        return ShipStatus.Instance.AllDoors.Where(d => d.Room == room).ToList();
    }

    // Retourne le statut global des portes dans une salle spécifiée
    public static string GetStatusOfDoorsInRoom(SystemTypes room, bool colorize)
    {
        var doorsInRoom = GetDoorsInRoom(room);
        if (doorsInRoom.Count <= 0) return "N/D";
        if (doorsInRoom.All(d => d.IsOpen)) return colorize ? "<color=#00FF00>Ouvert</color>" : "Ouvert";
        if (doorsInRoom.All(d => !d.IsOpen)) return colorize ? "<color=#FF0000>Fermé</color>" : "Fermé";
        return colorize ? "<color=#FFFF00>Mixte</color>" : "Mixte";
    }

    // Ouvre toutes les portes dans une salle spécifiée
    public static void OpenDoorsInRoom(SystemTypes doorRoom)
    {
        foreach (var door in GetDoorsInRoom(doorRoom))
        {
            OpenDoor(door);
        }
    }

    // Ferme toutes les portes dans une salle spécifiée
    public static void CloseDoorsInRoom(SystemTypes doorRoom)
    {
        try { ShipStatus.Instance.RpcCloseDoorsOfType(doorRoom); } catch { }
    }

    // Ouvre toutes les portes de la carte
    public static void OpenAllDoors()
    {
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            OpenDoor(door);
        }
    }

    // Ferme toutes les portes de la carte
    public static void CloseAllDoors()
    {
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            try { ShipStatus.Instance.RpcCloseDoorsOfType(door.Room); } catch { }
        }
    }

    // Ouvre une porte spécifique
    public static void OpenDoor(OpenableDoor openableDoor)
    {
        try { ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, (byte)(openableDoor.Id | 64)); } catch { }
    }
}
