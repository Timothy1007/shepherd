using System.Reflection;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.Data;
using UnityEngine;

namespace Shepherd.Battle.Tests
{
    /// <summary>BattleConfig（ScriptableObject）的預設值編譯後必須與規格第五節的預設值相同。</summary>
    public sealed class DataTests
    {
        [Test]
        public void BattleConfigDefaults_CompileToSpecDefaults()
        {
            var config = ScriptableObject.CreateInstance<BattleConfig>();
            try
            {
                var compiled = RuleSetCompiler.Compile(config).Params;
                var expected = new RuleParams();
                foreach (var f in typeof(RuleParams).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.AreEqual(f.GetValue(expected), f.GetValue(compiled), f.Name);
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CoreAssembly_HasNoUnityEngineReference()
        {
            foreach (var r in typeof(RoundResolver).Assembly.GetReferencedAssemblies())
            {
                StringAssert.DoesNotStartWith("UnityEngine", r.Name);
                StringAssert.DoesNotStartWith("UnityEditor", r.Name);
            }
        }
    }
}
