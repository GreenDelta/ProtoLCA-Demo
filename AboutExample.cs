using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp {
    class AboutExample : Example {

        private readonly Channel channel;

        public AboutExample(Channel channel) {
            this.channel = channel;
        }

        public string Description() {
            return "Get the version and database name of the service";
        }

        public void Run() {

            var client = new AboutService.AboutServiceClient(channel);
            var about = client.About(new Empty());

            Log($"  connected to database: {about.Database}");
            Log($"  service version: {about.Version}");
            Log($"  minimum required version for compatibility: {about.MinSupportedVersion}");
        }
    }
}
