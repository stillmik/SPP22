using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Faker.BaseTypes;
using Main;

namespace Faker
{
    public class Faker : IFaker
    {
        private readonly List<Type> circularReferencesEncounter;
        private readonly List<Type> circularReferencesEncounter2;

        private Dictionary<Type, IGenerator> generators;
        private List<ConstructorInfo> list = new List<ConstructorInfo>();
        private int countOfException;
        private int constructor = 0;
        public Faker()
        {
            generators = new Dictionary<Type, IGenerator>
            {
                { typeof(bool), new BoolGenerator()},
                { typeof(byte), new ByteGenerator()},
                { typeof(char), new CharGenerator()},
                { typeof(DateTime), new DateGenerator()},
                { typeof(double), new DoubleGenerator()},
                { typeof(float), new FloatGenerator()},
                { typeof(int), new IntGenerator()},
                { typeof(long), new LongGenerator()},
                { typeof(short), new ShortGenerator()},
                { typeof(string), new StringGenerator()},
            };
            PluginLoader loader = new PluginLoader(generators);
            loader.LoadPluginGenerators();
            circularReferencesEncounter = new List<Type>();
            circularReferencesEncounter2 = new List<Type>();
        }

        public T Create<T>()
        {
            
            return (T)Create(typeof(T));
        }

        public object Create(Type type)
        {
            object instance;
            countOfException = 0;

            if (TryGenerateAbstract(type, out instance))
                return instance;

            if (TryGenerateKnown(type, out instance))
                return instance;

            if (TryGenerateEnum(type, out instance))
                return instance;

            if (TryGenerateArray(type, out instance))
                return instance;

            if (TryList(type, out instance))
                return instance;

            if (TryGenerateCls(type, out instance))
            {
                return instance;
            }


            return default;
        }

        private bool TryGenerateAbstract(Type type, out object instance)
        {
            instance = default;

            if (!type.IsAbstract)
                return false;

            return true;
        }

        private bool TryGenerateArray(Type type, out object instance)
        {
            instance = null;
            if (!type.IsArray)
                return false;

            instance = (new ArrayGenerator(this, type)).Create();
            
            return true;
        }

        private bool TryGenerateKnown(Type type, out object instance)
        {
            instance = null;
            if (generators.TryGetValue(type, out IGenerator generator))
            {
                instance = generator.Create();
                return true;                                                                                                                                                                                         
            }

            return false;
        }

        private bool TryGenerateEnum(Type type, out object instance)
        {
            instance = null;

            if (!type.IsEnum)
                return false;

            Array values = type.GetEnumValues();
            Random random = new Random();

            instance = values.GetValue(random.Next(0, values.Length));

            return true;
        }

        private bool TryList(Type type,out object instance)
        {
            instance = null;
            if (!type.IsGenericType)
                return false;

            if (!(type.GetGenericTypeDefinition() == typeof(List<>)))
                return false;
            var innerTypes = type.GetGenericArguments();
            Type gType = innerTypes[0];
            //Console.WriteLine(gType.Name);
            int count = new Random().Next(1,20);
            instance = Activator.CreateInstance(type);
            object[] arr = new object[1];
            for (int i = 0; i < count; i++)
            {
                arr[0] = Create(gType);
                type.GetMethod("Add").Invoke(instance, arr);
            }

            return true;
        }


        private bool TryGenerateCls(Type type, out object instance)
        {
            instance = null;

            if (!type.IsClass && !type.IsValueType)
                return false;

            if (circularReferencesEncounter.Contains(type))
            {
                if (circularReferencesEncounter2.Contains(type))
                {
                    instance = default;
                    return true;
                }
                circularReferencesEncounter2.Add(type);
            }
            else
            {
                circularReferencesEncounter.Add(type);
            }

            circularReferencesEncounter.Add(type);
            if (TryConstruct(type, out instance))
            {
                GenerateFillProps(instance, type);
                GenerateFillFields(instance, type);

                if (circularReferencesEncounter.Contains(type) && circularReferencesEncounter2.Contains(type))
                {
                    circularReferencesEncounter2.Remove(type);
                }
                else
                    circularReferencesEncounter.Remove(type);
                return true;
            }

            return false;
        }

        private bool TryConstruct(Type type, out object instance)
        {
            object[] prms = null;
            instance = null;
            try
            {
                if (TryGetMaxParamsConstructor(type, out ConstructorInfo ctn))
                {
                    prms = GenerateConstructorParams(ctn);
                    /*Console.WriteLine(prms.Length);*/
                    instance = ctn.Invoke(prms);
                    return true;
                }
            }
            catch
            {
                countOfException++;
                object[] parameters = new object[countOfException];
                if (prms != null)
                    for (int i = 0; i < prms.Length - countOfException; i++)
                    {
                        parameters[i] = prms[i];
                    }

                instance = list[parameters.Length - 1].Invoke(parameters);
                return true;
            }

            return false;
        }
        
        private bool TryGetMaxParamsConstructor(Type type, out ConstructorInfo ctn)
        {
            list.Clear();
            ctn = null;

            var ctns = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ctns.Length == 0)
                return false;

            for (int i = 0; i < ctns.Length; i++)
            {
                  
                    if (
                        ctn == null || ctns[i].GetParameters().Length > ctn.GetParameters().Length)
                    {
              
                        ctn = ctns[i];
                        list.Add(ctn);
                    }                    
            }

            if (ctn == null)
                return false;

            return true;
        }

        private void GenerateFillProps(object instance, Type type)
        {
            var props = type.GetProperties();

            foreach (var prop in props)
            {
                if (!prop.CanWrite)
                    continue;

                if (prop.GetSetMethod() == null)
                    continue;

                prop.SetValue(instance, Create(prop.PropertyType));
            }
        }

        private void  GenerateFillFields(object instance, Type type)
        {
            var fields = type.GetFields( BindingFlags.Instance | BindingFlags.Public);
            
            foreach (var field in fields)
            {
                var value = Create(field.FieldType);
                field.SetValue(instance, value);
            }
        }

        private object[] GenerateConstructorParams(ConstructorInfo constructor)
        {
            
            var prms = constructor.GetParameters();
            object[] generated = new object[prms.Length];

            
            for (int i = 0; i < prms.Length; i++)
            {
                var p = prms[i];
                generated[i] = Create(p.ParameterType);
            }
            
            return generated;
        }
    }
}