using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace HorizonSync
{
    public class LocationResult
    {
        public double? Latitude, Longitude;
        public string Message;
        public static LocationResult Read()
        {
            try
            {
                using (var watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default))
                {
                    // Respect existing Windows permissions, never change privacy settings or request a prompt.
                    watcher.TryStart(true, TimeSpan.FromSeconds(8));
                    var position = watcher.Position;
                    if (!position.Location.IsUnknown)
                        return new LocationResult { Latitude = position.Location.Latitude, Longitude = position.Location.Longitude,
                            Message = Localization.T("已采用 Windows 位置（系统记录时间：") + position.Timestamp.ToLocalTime().ToString("MM-dd HH:mm") + "）" };
                    return new LocationResult { Message = Localization.T("Windows 暂未提供位置，保留已有坐标。可检查 Windows 定位权限，或手动填写坐标。") };
                }
            }
            catch (Exception e) { return new LocationResult { Message = Localization.T("系统位置不可用，保留已有坐标：") + e.Message }; }
        }
    }

}
