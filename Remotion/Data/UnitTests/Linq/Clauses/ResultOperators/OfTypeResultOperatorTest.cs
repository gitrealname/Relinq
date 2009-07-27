// This file is part of the re-motion Core Framework (www.re-motion.org)
// Copyright (C) 2005-2009 rubicon informationstechnologie gmbh, www.rubicon.eu
// 
// The re-motion Core Framework is free software; you can redistribute it 
// and/or modify it under the terms of the GNU Lesser General Public License 
// version 3.0 as published by the Free Software Foundation.
// 
// re-motion is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-motion; if not, see http://www.gnu.org/licenses.
// 
using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Remotion.Data.Linq.Clauses;
using Remotion.Data.Linq.Clauses.ExecutionStrategies;
using Remotion.Data.Linq.Clauses.ResultOperators;
using Remotion.Data.Linq.Clauses.StreamedData;
using Remotion.Data.UnitTests.Linq.TestDomain;
using Remotion.Utilities;

namespace Remotion.Data.UnitTests.Linq.Clauses.ResultOperators
{
  [TestFixture]
  public class OfTypeResultOperatorTest
  {
    private OfTypeResultOperator _resultOperator;

    [SetUp]
    public void SetUp ()
    {
      _resultOperator = new OfTypeResultOperator (typeof (GoodStudent));
    }

    [Test]
    public void Clone ()
    {
      var clonedClauseMapping = new QuerySourceMapping ();
      var cloneContext = new CloneContext (clonedClauseMapping);
      var clone = _resultOperator.Clone (cloneContext);

      Assert.That (clone, Is.InstanceOfType (typeof (OfTypeResultOperator)));
      Assert.That (((OfTypeResultOperator) clone).SearchedItemType, Is.SameAs (_resultOperator.SearchedItemType));
    }

    [Test]
    public void ExecuteInMemory ()
    {
      var student1 = new GoodStudent ();
      var student2 = new GoodStudent ();
      var student3 = new Student ();
      IEnumerable items = new Student[] { student1, student2, student3 };
      var itemExpression = Expression.Constant (student3, typeof (Student));
      IStreamedData input = new StreamedSequence (items, itemExpression);

      var result = _resultOperator.ExecuteInMemory (input);

      var sequence = result.GetCurrentSequenceInfo<GoodStudent> ();
      Assert.That (sequence.Sequence.ToArray (), Is.EquivalentTo (new[] { student1, student2 }));
      Assert.That (sequence.ItemExpression.Type, Is.EqualTo (typeof (GoodStudent)));
      Assert.That (((UnaryExpression) sequence.ItemExpression).Operand, Is.SameAs (itemExpression));
    }

    [Test]
    public void ExecutionStrategy ()
    {
      Assert.That (_resultOperator.ExecutionStrategy, Is.SameAs (CollectionExecutionStrategy.Instance));
    }

    [Test]
    public void GetResultType ()
    {
      Assert.That (_resultOperator.GetResultType (typeof (IQueryable<Student>)), Is.SameAs (typeof (IQueryable<GoodStudent>)));
    }

    [Test]
    [ExpectedException (typeof (ArgumentTypeException))]
    public void GetResultType_InvalidType ()
    {
      _resultOperator.GetResultType (typeof (Student));
    }
  }
}