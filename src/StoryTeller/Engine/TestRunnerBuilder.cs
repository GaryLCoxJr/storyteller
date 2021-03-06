﻿using System;
using FubuCore.Conversion;
using FubuCore.Util;
using StoryTeller.Model;
using StructureMap;
using FubuCore;

namespace StoryTeller.Engine
{
    public class TestRunnerBuilder
    {
        private readonly ISystem _system;
        private readonly IFixtureObserver _observer;

        public TestRunnerBuilder(ISystem system, IFixtureObserver observer)
        {
            _system = system;
            _observer = observer;
        }

        public static ITestRunner ForSystem<T>() where T : ISystem, new()
        {
            var system = new T();
            return ForSystem(system);
        }

        private static ITestRunner ForSystem(ISystem system)
        {
            var registry = new FixtureRegistry();
            registry.AddFixturesFromAssembly(system.GetType().Assembly);

            var builder = new TestRunnerBuilder(system, new NulloFixtureObserver());
            return builder.Build();
        }

        public static ITestRunner ForFixture<T>() where T : IFixture
        {
            return For(r => r.AddFixture<T>());
        }

        public static ITestRunner For(Action<FixtureRegistry> configure)
        {
            return new TestRunnerBuilder(new NulloSystem(configure), new NulloFixtureObserver()).Build();
        }

        public static IContainer BuildFixtureContainer(ISystem system)
        {
            var container = new Container();
            var rfc = system as IRequireFixtureContainer;
            if( rfc != null )
            {
                rfc.ConfigureFixtureContainer(container);
            }

            return container;
        }

        public ITestRunner Build()
        {
            var container = BuildFixtureContainer(_system);
            var registry = new FixtureRegistry();
            _system.RegisterFixtures(registry);
            registry.AddFixturesToContainer(container);
            var source = new FixtureContainerSource(container);
            var nestedContainer = source.Build();
            var observer = _observer;

            var library = BuildLibrary(new SystemLifecycle(_system), observer, nestedContainer, new CompositeFilter<Type>(), _system.BuildConverter());
            
            return new TestRunner(_system, library, source);
        }

        public static FixtureLibrary BuildLibrary(SystemLifecycle lifeCycle, IFixtureObserver observer, IContainer container, CompositeFilter<Type> filter, IObjectConverter converter)
        {
            if (converter == null) throw new ArgumentNullException("converter");

            try
            {
                var builder = new LibraryBuilder(observer, filter, converter);
                observer.RecordStatus("Starting to rebuild the fixture model");

                container.Inject<IObjectConverter>(converter);
                var context = new TestContext(container);

                observer.RecordStatus("Setting up the system environment");
                lifeCycle.StartApplication();


                lifeCycle.SetupEnvironment();
                observer.RecordStatus("Registering the system services");
                lifeCycle.RegisterServices(context);

                observer.RecordStatus("Starting to read fixtures");
                return builder.Build(context);
            }
            finally
            {
                observer.RecordStatus("Finished rebuilding the fixture model");
            }
        }
    }
}