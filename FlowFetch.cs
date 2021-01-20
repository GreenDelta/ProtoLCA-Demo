using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using FlowMapService = ProtoLCA.Services.FlowMapService.FlowMapServiceClient;
using DataService = ProtoLCA.Services.DataService.DataServiceClient;

namespace DemoApp
{
    class FlowFetch
    {
        private readonly MD5 md5;
        private readonly FlowMap flowMap;
        private readonly DataService data;
        private readonly FlowMapService mappings;

        public FlowFetch(Channel chan, string mappingName)
        {
            md5 = MD5.Create();
            data = new DataService(chan);
            mappings = new FlowMapService(chan);
            flowMap = GetFlowMap(mappingName, mappings);
        }

        private FlowMap GetFlowMap(string name, FlowMapService service)
        {
            var status = service.Get(new FlowMapInfo
            {
                Name = name
            });

            if (status.Ok)
                return status.FlowMap;
            var map = new FlowMap
            {
                Name = name,
                Id = Guid.NewGuid().ToString()
            };
            return map;
        }

        private string MakeID(params string[] args)
        {
            var path = "";
            for (int i = 0; i < args.Length; i++)
            {
                var part = args[i] == null
                    ? ""
                    : args[i].Trim().ToLower();
                if (i != 0)
                {
                    path += " - ";
                }
                path += part;
            }
            var hash = md5.ComputeHash(
                Encoding.UTF8.GetBytes(path));
            return new Guid(hash).ToString();
        }

        public async Task<(Ref, double)> ElementaryFlow(
            string name, string unit, string compartment = "")
        {
            var info = $"{name}/{unit}/{compartment}";
            var flowID = MakeID(name, unit, compartment);

            // try to find a mapped flow
            foreach (var m in flowMap.Mappings)
            {
                if (flowID.Equals(m.From.Flow.Id))
                {
                    Console.WriteLine($"Found mapping for {info}");
                    return (m.To.Flow, m.ConversionFactor);
                }
            }

            // search for a flow
            var search = data.Search(new SearchRequest
            {
                Type = ModelType.Flow,
                Query = name,
            }).ResponseStream;
            Ref candidate = null;
            while (await search.MoveNext())
            {
                // check the unit and the compartment
                if (IsBetterMatch(candidate, search.Current, name, compartment))
                {
                    candidate = search.Current;
                }
            }

            if (candidate != null)
            {
                Console.WriteLine(
                    $"Found matching flow {candidate.Id} for {info}");
            }


            return (candidate, 1.0);

        }

        // Try to determine if the given candidate is a better match than
        // the current flow regarding the name and compartment path.
        private bool IsBetterMatch(
            Ref current, Ref candidate, string name, string compartment)
        {
            if (current == null)
                return true;

            int currentScore = 0;
            int candidateScore = 0;

            var currentName = current.Name.ToLower();
            var candidateName = candidate.Name.ToLower();
            var words = name.Split(' ');
            foreach (var word in words)
            {
                var w = word.Trim().ToLower();
                if (string.IsNullOrEmpty(w))
                    continue;
                if (currentName.Contains(w))
                {
                    currentScore += w.Length;
                }
                if (candidateName.Contains(w))
                {
                    candidateScore += w.Length;
                }
            }

            if (candidateScore != currentScore
                || string.IsNullOrWhiteSpace(compartment))
                return candidateScore > currentScore;

            var currentPath = current.CategoryPath;
            var candidatePath = candidate.CategoryPath;
            var segments = compartment.Split('/');
            foreach (var segment in segments)
            {
                var s = segment.Trim().ToLower();
                if (string.IsNullOrEmpty(s))
                    continue;
                foreach (var p in currentPath)
                {
                    if (p.ToLower().Contains(s))
                    {
                        currentScore += s.Length;
                        break;
                    }
                }
                foreach (var p in candidatePath)
                {
                    if (p.ToLower().Contains(s))
                    {
                        candidateScore += s.Length;
                        break;
                    }
                }
            }
            return candidateScore > currentScore;
        }
    }
}
