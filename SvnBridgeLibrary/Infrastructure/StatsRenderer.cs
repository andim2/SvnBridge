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

        public void Render(IHttpContext context)
        {
            using (TextWriter writer = new StreamWriter(context.Response.OutputStream))
            {
                writer.Write("<html><head><title>SvnBridge Stats</title></head>");
                writer.Write("<h1>Statistics</h1>");
                writer.Write("<table>");
                foreach (var stat in actionTracking.GetStatistics())
                {
                    writer.Write("<tr><td>");
                    writer.Write(stat.Key);
                    writer.Write("</td><td>");
                    writer.Write(stat.Value);
                    writer.Write("</td></tr>");
                }
                writer.Write("</table>");
                writer.Write("</html>");
            }
        }
    }
}