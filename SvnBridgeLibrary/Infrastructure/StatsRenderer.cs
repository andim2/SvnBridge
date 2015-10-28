using System.IO;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.Interfaces;

namespace SvnBridge.Handlers.Renderers
{
    public class StatsRenderer
    {
        private readonly ActionTrackingViaPerfCounter actionTracking;

        public StatsRenderer(ActionTrackingViaPerfCounter actionTracking)
        {
            this.actionTracking = actionTracking;
        }

        public void Render(IHttpContext context, StreamWriter output)
        {
                output.Write("<html><head><title>SvnBridge Stats</title></head>");
                output.Write("<h1>Statistics</h1>");
                output.Write("<table>");
                foreach (var stat in actionTracking.GetStatistics())
                {
                    output.Write("<tr><td>");
                    output.Write(stat.Key);
                    output.Write("</td><td>");
                    output.Write(stat.Value);
                    output.Write("</td></tr>");
                }
                output.Write("</table>");
                output.Write("</html>");
        }
    }
}
