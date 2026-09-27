using System;
using System.Collections.Generic;
using Hazel;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Camera jammer (host-only): players using security cameras get a personal
	// comms glitch while they watch, restored when they stop.
	// Ported from othermenu Cheats/CameraJammer.cs.
	internal static class CameraJammer
	{
		private const int HandlingId = 20044;
		private const float Interval = 0.5f;
		private const byte SecuritySystem = 11;
		private const byte CommsSystem = 14;

		private static readonly HashSet<byte> _cur = new HashSet<byte>();
		private static readonly HashSet<byte> _prev = new HashSet<byte>();

		private static float _next;

		internal static void Tick()
		{
			try
			{
				if(Time.unscaledTime < _next)
					return;
				_next = Time.unscaledTime + Interval;

				bool active = CheatToggles.cameraJam
					&& AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
					&& ShipStatus.Instance != null && MeetingHud.Instance == null;

				if(!active)
				{
					FixAll();
					return;
				}

				SecurityCameraSystemType cams = Cameras();
				if(cams == null || !CommsSupported())
					return;

				int hostId = AmongUsClient.Instance.HostId;
				_cur.Clear();

				var it = cams.PlayersUsing.GetEnumerator();
				while(it.MoveNext())
				{
					byte pid = it.Current;
					PlayerControl pc = ById(pid);
					if(pc == null)
						continue;
					if(pc == PlayerControl.LocalPlayer || pc.OwnerId == hostId)
						continue;
					_cur.Add(pid);
				}

				foreach(byte pid in _cur)
					if(!_prev.Contains(pid))
						SetComms(ById(pid), true);

				foreach(byte pid in _prev)
					if(!_cur.Contains(pid)) SetComms(ById(pid), false);

				_prev.Clear();
				foreach(byte pid in _cur)
					_prev.Add(pid);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "CameraJammer.Tick: jamming camera watchers"); }
		}

		private static void FixAll()
		{
			try
			{
				if(_prev.Count == 0)
					return;

				foreach(byte pid in _prev)
					SetComms(ById(pid), false);

				_prev.Clear();
				_cur.Clear();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "CameraJammer.FixAll: restoring jammed players"); }
		}

		private static PlayerControl ById(byte pid)
		{
			var e = PlayerControl.AllPlayerControls.GetEnumerator();
			while(e.MoveNext())
			{
				PlayerControl p = e.Current;
				if(p != null && p.PlayerId == pid)
					return p;
			}
			return null;
		}

		private static void SetComms(PlayerControl pc, bool broken)
		{
			try
			{
				if(pc == null || ShipStatus.Instance == null) return;

				MessageWriter body = MessageWriter.Get(SendOption.Reliable);
				try
				{
					body.StartMessage(CommsSystem);
					body.Write((byte)(broken ? 1 : 0));
					body.EndMessage();

					var batch = new Network.BatchedMessage(pc.OwnerId);
					batch.QueueDataFlag(((InnerNet.InnerNetObject)ShipStatus.Instance).NetId, body);
					batch.FinishBatch();
				}
				finally
				{
					body.Recycle();
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "CameraJammer.SetComms: sending targeted comms glitch"); }
		}

		private static SecurityCameraSystemType Cameras()
		{
			try
			{
				ISystemType sys = ShipStatus.Instance.Systems[(SystemTypes)SecuritySystem];
				return sys != null ? sys.Cast<SecurityCameraSystemType>() : null;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "CameraJammer.Cameras: finding camera system"); return null; }
		}

		private static bool CommsSupported()
		{
			try
			{
				ISystemType sys = ShipStatus.Instance.Systems[(SystemTypes)CommsSystem];
				return sys != null && sys.Cast<HudOverrideSystemType>() != null;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "CameraJammer.CommsSupported: checking comms system"); return false; }
		}
	}
}
