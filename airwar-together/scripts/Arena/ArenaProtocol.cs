using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Airwar.Arena;

// Fixed, bounded binary messages. Never deserialize objects or arbitrary Variants from the network.
internal static class ArenaProtocol
{
    internal enum Kind : byte { Hello = 1, Welcome, Reject, Identity, Roster, Input, Warp, Ships, Shots, Notice, Ping, Pong, Leave }
    internal const int MaximumPacket = 1200;
    internal const int ShipsPerPacket = 12, ShotsPerPacket = 28;
    private const ushort Magic = 0x4157;
    private const byte Version = 2;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static byte[] Make(Kind kind, Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
        writer.Write(Magic); writer.Write(Version); writer.Write((byte)kind); write(writer);
        if (stream.Length > MaximumPacket) throw new InvalidDataException("Arena packet exceeds MTU budget");
        return stream.ToArray();
    }
    internal static BinaryReader Open(byte[] bytes, out Kind kind)
    {
        if (bytes.Length is < 4 or > MaximumPacket) throw new InvalidDataException("Invalid packet length");
        var reader = new BinaryReader(new MemoryStream(bytes, false), Utf8);
        if (reader.ReadUInt16() != Magic || reader.ReadByte() != Version) { reader.Dispose(); throw new InvalidDataException("Protocol mismatch"); }
        kind = (Kind)reader.ReadByte(); return reader;
    }
    internal static void End(BinaryReader reader)
    { if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing packet payload"); }
    internal static void Text(BinaryWriter writer, string text, int maximum)
    {
        byte[] data = Utf8.GetBytes(text);
        if (data.Length > maximum) throw new InvalidDataException("String too long");
        writer.Write((byte)data.Length); writer.Write(data);
    }
    internal static string Text(BinaryReader reader, int maximum)
    {
        int count = reader.ReadByte();
        if (count > maximum) throw new InvalidDataException("String too long");
        byte[] data = reader.ReadBytes(count); if (data.Length != count) throw new EndOfStreamException();
        return Utf8.GetString(data);
    }
    internal static void Vector(BinaryWriter writer, Vector3 v) { writer.Write(v.X); writer.Write(v.Y); writer.Write(v.Z); }
    internal static Vector3 Vector(BinaryReader reader) => new(Finite(reader), Finite(reader), Finite(reader));
    internal static float Finite(BinaryReader reader)
    { float value = reader.ReadSingle(); return float.IsFinite(value) ? value : throw new InvalidDataException("Non-finite state"); }
    internal static void Command(BinaryWriter writer, ArenaCommand command, uint sequence)
    {
        writer.Write(sequence); writer.Write(command.Pitch); writer.Write(command.Yaw); writer.Write(command.Roll); writer.Write(command.Throttle);
        writer.Write((byte)((command.Fire ? 1 : 0) | (command.Boost ? 2 : 0))); writer.Write((byte)command.Destination);
    }
    internal static ArenaCommand Command(BinaryReader reader, out uint sequence)
    {
        sequence = reader.ReadUInt32(); float pitch = Finite(reader), yaw = Finite(reader), roll = Finite(reader), throttle = Finite(reader);
        byte flags = reader.ReadByte(); int destination = reader.ReadByte();
        if (flags > 3 || destination >= SectorCatalog.Count) throw new InvalidDataException("Invalid input");
        return new ArenaCommand(pitch, yaw, roll, throttle, (flags & 1) != 0, (flags & 2) != 0, false, destination);
    }
    internal static void Ship(BinaryWriter writer, ArenaShip ship)
    {
        writer.Write(ship.PeerId); writer.Write((byte)ship.Sector); Vector(writer, ship.Position); Vector(writer, ship.Velocity);
        writer.Write(ship.Rotation.X); writer.Write(ship.Rotation.Y); writer.Write(ship.Rotation.Z); writer.Write(ship.Rotation.W);
        writer.Write(ship.Health); writer.Write(ship.Shield); writer.Write(ship.Energy); writer.Write(ship.Speed);
        writer.Write((byte)((ship.Alive ? 1 : 0) | (ship.Boosting ? 2 : 0)));
        writer.Write((ushort)Math.Min(65535, ship.Kills)); writer.Write((ushort)Math.Min(65535, ship.Deaths));
        writer.Write(ship.RespawnRemaining); writer.Write(ship.WarpCharge); writer.Write(ship.LastInput);
    }
    internal static ArenaShip Ship(BinaryReader reader)
    {
        var ship = new ArenaShip { PeerId = reader.ReadInt32(), Sector = reader.ReadByte(), Position = Vector(reader), Velocity = Vector(reader),
            Rotation = new Quaternion(Finite(reader), Finite(reader), Finite(reader), Finite(reader)),
            Health = Finite(reader), Shield = Finite(reader), Energy = Finite(reader), Speed = Finite(reader) };
        byte flags = reader.ReadByte(); ship.Alive = (flags & 1) != 0; ship.Boosting = (flags & 2) != 0;
        ship.Kills = reader.ReadUInt16(); ship.Deaths = reader.ReadUInt16(); ship.RespawnRemaining = Finite(reader);
        ship.WarpCharge = Finite(reader); ship.LastInput = reader.ReadUInt32();
        float rotationLength = ship.Rotation.LengthSquared();
        if (ship.PeerId <= 0 || ship.Sector >= SectorCatalog.Count || flags > 3 || rotationLength is < .5f or > 1.5f ||
            !WorldPosition(ship.Position) || ship.Velocity.LengthSquared() > 400 * 400 || ship.Health is < 0 or > 100 ||
            ship.Shield is < 0 or > 50 || ship.Energy is < 0 or > 100 || ship.Speed is < 0 or > 400 ||
            ship.RespawnRemaining is < 0 or > 5.1f || ship.WarpCharge is < 0 or > 3.1f)
            throw new InvalidDataException("Invalid ship state");
        ship.Rotation = ship.Rotation.Normalized(); return ship;
    }
    internal static void Shot(BinaryWriter writer, ArenaShot shot)
    { writer.Write(shot.Id); writer.Write(shot.Owner); writer.Write((byte)shot.Sector); Vector(writer, shot.Position); Vector(writer, shot.Velocity); writer.Write(shot.Life); }
    internal static ArenaShot Shot(BinaryReader reader)
    {
        var shot = new ArenaShot { Id = reader.ReadInt32(), Owner = reader.ReadInt32(), Sector = reader.ReadByte(),
            Position = Vector(reader), Velocity = Vector(reader), Life = Finite(reader) };
        // A projectile can outlive its owner and travel beyond the play boundary,
        // but it cannot exceed its 1.8-second lifetime and 1430 m/s launch speed.
        if (shot.Id <= 0 || shot.Owner <= 0 || shot.Sector >= SectorCatalog.Count || !WorldPosition(shot.Position, 14000) ||
            shot.Velocity.LengthSquared() > 1500 * 1500 || shot.Life is <= 0 or > 1.81f)
            throw new InvalidDataException("Invalid projectile state");
        return shot;
    }
    private static bool WorldPosition(Vector3 position, float limit = 12000) =>
        Math.Abs(position.X) <= limit && Math.Abs(position.Y) <= limit && Math.Abs(position.Z) <= limit;
    internal static string CleanName(string name)
    {
        var builder = new StringBuilder();
        foreach (char c in name.Trim())
        { if (!char.IsControl(c) && c != '[' && c != ']' && !char.IsSurrogate(c) && Utf8.GetByteCount(builder.ToString() + c) <= 48) builder.Append(c); }
        return builder.Length == 0 ? "Pilot" : builder.ToString();
    }
    internal static string CleanShip(string id) => id is "spectre" or "ember" or "wraith" or "stratus" or "manta" or "raptor" or "ion" or "flare" or "nebula" or "arctic" or "jade" or "gold" ? id : "spectre";
}
