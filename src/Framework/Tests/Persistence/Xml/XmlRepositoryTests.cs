using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using N2.Details;
using N2.Persistence;
using N2.Persistence.NH;
using N2.Tests.Persistence.Definitions;
using NUnit.Framework;
using Shouldly;
using N2.Persistence.Xml;
using N2.Persistence.Serialization;

namespace N2.Tests.Persistence.NH
{
    [TestFixture, Category("Integration")]
    public class XmlRepositoryTests : ItemTestsBase
    {
		XmlContentRepository repository;

        protected override T CreateOneItem<T>(int id, string name, ContentItem parent)
        {
            var item = base.CreateOneItem<T>(id, name, parent);
            repository.SaveOrUpdate(item);
            return item;
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
			var definitions = TestSupport.SetupDefinitions(typeof(Definitions.PersistableItem), typeof(Definitions.PersistablePart));
			var writer = new ItemXmlWriter(
						definitions,
						TestSupport.SetupUrlParser(),
						TestSupport.SetupFileSystem());
			var reader = new ItemXmlReader(
						definitions,
						TestSupport.SetupContentActivator());
            repository = new XmlContentRepository(definitions, writer, reader);
        }

        [Test]
        public void CanSave()
        {
            int itemID = SaveAnItem("savedItem", null);
            Assert.AreNotEqual(0, itemID);

            using (repository)
            {
                ContentItem item = repository.Get(itemID);
                Assert.AreEqual(item.ID, itemID);
                repository.Delete(item);
                repository.Flush();
            }
        }

        [Test]
        public void CanUpdate()
        {
            int itemID = SaveAnItem("savedItem", null);

            using (repository)
            {
                ContentItem item = repository.Get(itemID);
                item.Title = "updated item";
                repository.SaveOrUpdate(item);
                repository.Flush();
            }

            using (repository)
            {
                ContentItem item = repository.Get(itemID);
                Assert.AreEqual("updated item", item.Title);
                repository.Delete(item);
                repository.Flush();
            }
        }

        [Test]
        public void CanDelete()
        {
            int itemID = SaveAnItem("itemToDelete", null);

            using (repository)
            {
                ContentItem item = repository.Get(itemID);
                Assert.IsNotNull(item, "There should be a saved item.");
                repository.Delete(item);
                repository.Flush();
            }

            using (repository)
            {
                ContentItem item = repository.Get(itemID);
                Assert.IsNull(item, "Item is supposed to be deleted");
                repository.Flush();
            }
        }

		[Test]
		public void Parent_IsRestored()
		{
			int parentID = SaveAnItem("parent", null);
			int itemID = SaveAnItem("child", repository.Get(parentID));

			using (repository)
			{
				var parent = repository.Get(parentID);
				var child = repository.Get(itemID);
				child.Parent.ShouldBe(parent);
			}
		}

        [Test]
        public void ItemClasses_MayHaveNonVirtualProperties()
        {
            using (repository)
            {
                ContentItem item = CreateOneItem<Definitions.NonVirtualItem>(0, "item", null);
				repository.SaveOrUpdate(item);
                repository.Flush();

                repository.Delete(item);
                repository.Flush();
            }
        }

        [Test]
        public void FindDiscriminatorsBelow_FindsDistinctDiscriminators()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "root", null);
            ContentItem child = CreateOneItem<Definitions.PersistableItem>(0, "item", root);
			repository.SaveOrUpdate(root);

            repository.FindDescendantDiscriminators(root).Single().Discriminator
                .ShouldBe("PersistableItem");
        }

        [Test]
        public void FindDiscriminators_FindsDistinctDiscriminators_Without()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "root", null);
			repository.SaveOrUpdate(root);
            ContentItem child = CreateOneItem<Definitions.PersistableItem>(0, "item", null);
			repository.SaveOrUpdate(child);

            var d = repository.FindDescendantDiscriminators(null).Single();
            d.Discriminator.ShouldBe("PersistableItem");
            d.Count.ShouldBe(2);
        }

        [Test]
        public void FindDiscriminatorsBelow_FindsAncestorDiscriminator()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child = CreateOneItem<Definitions.PersistablePart>(0, "part", root);
            repository.SaveOrUpdate(root, child);

            var discriminators = repository.FindDescendantDiscriminators(root).ToList();
            var discriminator = discriminators.First(d => d.Discriminator == "PersistablePart");
            discriminator.Count.ShouldBe(1);
        }

        [Test]
        public void FindDiscriminatorsBelow_FindsRootDiscriminator()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child = CreateOneItem<Definitions.PersistablePart>(0, "part", root);
            repository.SaveOrUpdate(root, child);

            var discriminators = repository.FindDescendantDiscriminators(root).ToList();
            var discriminator = discriminators.First(d => d.Discriminator == "PersistableItem");
            discriminator.Count.ShouldBe(1);
        }

        [Test]
        public void FindDiscriminator_Count()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child1 = CreateOneItem<Definitions.PersistableItem>(0, "item1", root);
            ContentItem child2 = CreateOneItem<Definitions.PersistableItem>(0, "item2", root);
            repository.SaveOrUpdate(root, child1, child2);

            var discriminators = repository.FindDescendantDiscriminators(root).ToList();
            discriminators.Single().Count.ShouldBe(3);
        }

        [Test]
        public void FindDescendantDiscriminators_OnMultipleLevels()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child1 = CreateOneItem<Definitions.PersistableItem>(0, "item1", root);
            ContentItem child2 = CreateOneItem<Definitions.PersistableItem>(0, "item2", child1);
            ContentItem child3 = CreateOneItem<Definitions.PersistableItem>(0, "item3", child2);
            repository.SaveOrUpdate(root, child1, child2, child3);

            var discriminators = repository.FindDescendantDiscriminators(root).ToList();
            discriminators.Single().Count.ShouldBe(4);
        }

        [Test]
        public void FindDescendantDiscriminators_IsSortedByNumberOfItmes()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child1 = CreateOneItem<Definitions.PersistablePart>(0, "item1", root);
            ContentItem child2 = CreateOneItem<Definitions.PersistablePart>(0, "item2", child1);
            ContentItem child3 = CreateOneItem<Definitions.PersistablePart>(0, "item3", child2);
            repository.SaveOrUpdate(root, child1, child2, child3);

            var discriminators = repository.FindDescendantDiscriminators(root).ToList();
            discriminators[0].Discriminator.ShouldBe("PersistablePart");
            discriminators[1].Discriminator.ShouldBe("PersistableItem");
        }

        [Test]
        public void FindDescends_FindsAncestorOfType()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child1 = CreateOneItem<Definitions.PersistablePart>(0, "item1", root);
            ContentItem child2 = CreateOneItem<Definitions.PersistablePart>(0, "item2", child1);
            ContentItem child3 = CreateOneItem<Definitions.PersistablePart>(0, "item3", child2);
            repository.SaveOrUpdate(root, child1, child2, child3);

            var discriminators = repository.FindDescendants(root, "PersistablePart");
            discriminators.Count().ShouldBe(3);
        }
        
        [Test]
        public void FindDescends_FindsDescendantsOfType()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child1 = CreateOneItem<Definitions.PersistablePart>(0, "item1", root);
            ContentItem child2 = CreateOneItem<Definitions.PersistablePart>(0, "item2", child1);
            repository.SaveOrUpdate(root, child1, child2);

            var discriminators = repository.FindDescendants(root, "PersistableItem");
            discriminators.Count().ShouldBe(1);
        }

        [Test]
        public void FindDescends_WithNull_FinsAllInDb()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child1 = CreateOneItem<Definitions.PersistableItem>(0, "item1", null);
            repository.SaveOrUpdate(root, child1);

            var discriminators = repository.FindDescendants(null, "PersistableItem");
            discriminators.Count().ShouldBe(2);
        }

        [Test]
        public void Find_TypeAndParent_ShouldOnlyInclude_ItemWithSpecified_TypeAndParent()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            ContentItem child1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", root);
            ContentItem child2 = CreateOneItem<Definitions.PersistablePart>(0, "part2", root);

            var results = repository.Find(new Parameter("class", "PersistableItem"), new Parameter("Parent", root));

            results.Single().ShouldBe(child1);
        }

        [Test]
        public void FindReferencing_ShouldReturn_ItemsThatLinkToTarget()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            var child1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", root);
            var child2 = CreateOneItem<Definitions.PersistableItem>(0, "page2", root);

            child1["Link"] = child2;
            child2["Link"] = child1;

            repository.SaveOrUpdate(root);
            repository.Flush();

            var results = repository.FindReferencing(child2);

            results.Single().ShouldBe(child1);
        }

        [Test]
        public void FindReferencing_ShouldReturn_ItemsThatLinkToTarget_InDetailCollection()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            var child1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", root);
            var child2 = CreateOneItem<Definitions.PersistableItem>(0, "page2", root);

            child1.GetDetailCollection("Links", true).Add(child2);
            child2.GetDetailCollection("Links", true).Add(child1);

			repository.SaveOrUpdate(root);
            repository.Flush();

            var results = repository.FindReferencing(child2);

            results.Single().ShouldBe(child1);
        }

        [Test]
        public void RemoveReferencesTo_ShouldRemove_LinkFromOtherItem()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            var child1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", root);
            var child2 = CreateOneItem<Definitions.PersistableItem>(0, "page2", root);
            child1["Link"] = child2;
            child2["Link"] = child1;
            repository.SaveOrUpdate(root);
            repository.Flush();

            repository.RemoveReferencesToRecursive(child2);

            child1["Link"].ShouldBe(null);
        }

        [Test]
        public void RemoveReferencesTo_ShouldRemove_LinkToDescendantItem_FromOtherItem()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            var child1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", root);
            var grandchild1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", child1);
            var child2 = CreateOneItem<Definitions.PersistableItem>(0, "page2", root);
            child1["Link"] = grandchild1;
            grandchild1["Link"] = grandchild1;
            child2["Link"] = grandchild1;
            repository.SaveOrUpdate(root);
            repository.Flush();

            repository.RemoveReferencesToRecursive(child1);

            child2["Link"].ShouldBe(null);
        }

        [Test]
        public void RemoveReferencesTo_ShouldRemove_LinkToDescendantItem_FromItself()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            var child1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", root);
            var grandchild1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", child1);
            var child2 = CreateOneItem<Definitions.PersistableItem>(0, "page2", root);
            child1["Link"] = grandchild1;
            grandchild1["Link"] = grandchild1;
            child2["Link"] = grandchild1;
            repository.SaveOrUpdate(root);
            repository.Flush();

            repository.RemoveReferencesToRecursive(child1);

            grandchild1["Link"].ShouldBe(null);
        }

        [Test]
        public void RemoveReferencesTo_ShouldRemove_LinkToDescendantItem_FromParent()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            var child1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", root);
            var grandchild1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", child1);
            var child2 = CreateOneItem<Definitions.PersistableItem>(0, "page2", root);
            child1["Link"] = grandchild1;
            grandchild1["Link"] = grandchild1;
            child2["Link"] = grandchild1;
            repository.SaveOrUpdate(root);
            repository.Flush();

            repository.RemoveReferencesToRecursive(child1);

            child1["Link"].ShouldBe(null);
        }

        [Test]
        public void RemoveReferencesTo_ShouldShouldReturn_NumberOfRemovedReferences()
        {
            ContentItem root = CreateOneItem<Definitions.PersistableItem>(0, "page", null);
            var child1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", root);
            var grandchild1 = CreateOneItem<Definitions.PersistableItem>(0, "page1", child1);
            var child2 = CreateOneItem<Definitions.PersistableItem>(0, "page2", root);
            child1["Link"] = grandchild1;
            grandchild1["Link"] = grandchild1;
            child2["Link"] = grandchild1;
            repository.SaveOrUpdate(root);
            repository.Flush();

            int count = repository.RemoveReferencesToRecursive(child1);

            count.ShouldBe(3);
        }

        private int SaveAnItem(string name, ContentItem parent)
        {
            using (repository)
            {
                ContentItem item = CreateOneItem<Definitions.PersistableItem>(0, name, parent);
                repository.SaveOrUpdate(item);
                repository.Flush();
                return item.ID;
            }
        }
    }
}
