using System;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using Rubicon.Collections;
using Rubicon.Data.Linq.Clauses;
using Rubicon.Data.Linq.Parsing;
using Rubicon.Data.Linq.Parsing.Structure;
using Rubicon.Data.Linq.UnitTests.TestQueryGenerators;

namespace Rubicon.Data.Linq.UnitTests.ParsingTest.StructureTest
{
  [TestFixture]
  public class QueryExpressionCreatorTest
  {
    private IQueryable<Student> _source;
    private Expression _root;
    private ParseResultCollector _result;
    private QueryExpressionCreator _expressionCreator;
    private FromExpression _firstFromExpression;

    [SetUp]
    public void SetUp ()
    {
      _source = ExpressionHelper.CreateQuerySource();
      _root = ExpressionHelper.CreateExpression();
      _result = new ParseResultCollector (_root);
      _firstFromExpression = new FromExpression (Expression.Constant (_source), ExpressionHelper.CreateParameterExpression());
      _result.AddBodyExpression (_firstFromExpression);
      _expressionCreator = new QueryExpressionCreator (_root, _result);
    }

    [Test]
    [ExpectedException (typeof (ParserException), ExpectedMessage = "There is no projection for the select clause.")]
    public void NoProjectionForSelectClause ()
    {
      _expressionCreator.CreateQueryExpression ();
    }

    [Test]
    public void ResultType_Simple ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());
      QueryExpression expression = _expressionCreator.CreateQueryExpression ();
      Assert.AreEqual (_root.Type, expression.ResultType);
    }

    [Test]
    public void ResultType_WithProjection ()
    {
      IQueryable<Tuple<Student, string, string, string>> query =
          SelectTestQueryGenerator.CreateSimpleQueryWithSpecialProjection (ExpressionHelper.CreateQuerySource());
      QueryExpressionCreator expressionCreator = new QueryExpressionCreator (query.Expression, _result);

      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());
      QueryExpression expression = expressionCreator.CreateQueryExpression ();
      Assert.AreEqual (typeof (IQueryable<Tuple<Student, string, string, string>>), expression.ResultType);
    }

    [Test]
    public void FirstBodyClause_TranslatedIntoMainFromClause ()
    {
      var additionalFromExpression = new FromExpression (ExpressionHelper.CreateLambdaExpression(), ExpressionHelper.CreateParameterExpression());
      _result.AddBodyExpression (additionalFromExpression);
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());

      QueryExpression expression = _expressionCreator.CreateQueryExpression ();
      Assert.IsNotNull (expression.MainFromClause);
      Assert.AreSame (_firstFromExpression.Identifier, expression.MainFromClause.Identifier);
      Assert.AreSame (_firstFromExpression.Expression, expression.MainFromClause.QuerySource);
      Assert.AreNotSame (additionalFromExpression.Identifier, expression.MainFromClause.Identifier);
    }

    [Test]
    public void LastProjectionExpresion_TranslatedIntoSelectClause_NoFromClauses ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      
      QueryExpression expression = _expressionCreator.CreateQueryExpression();
      Assert.AreEqual (0, expression.BodyClauses.Count);

      SelectClause selectClause = expression.SelectOrGroupClause as SelectClause;
      Assert.IsNotNull (selectClause);
      Assert.AreSame (_result.ProjectionExpressions[0], selectClause.ProjectionExpression);
    }

    [Test]
    public void SelectClause_Distinct_True ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());
      _result.SetDistinct();

      QueryExpression expression = _expressionCreator.CreateQueryExpression ();
      
      SelectClause selectClause = expression.SelectOrGroupClause as SelectClause;
      Assert.IsTrue (selectClause.Distinct);
    }

    [Test]
    public void SelectClause_Distinct_False ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());
      QueryExpression expression = _expressionCreator.CreateQueryExpression ();

      SelectClause selectClause = expression.SelectOrGroupClause as SelectClause;
      Assert.IsFalse (selectClause.Distinct);
    }

    [Test]
    [ExpectedException (typeof (ParserException), ExpectedMessage = "From expression 'i' (() => 0) doesn't have a projection expression.")]
    public void FromExpression_WithoutProjection ()
    {
      var additionalFromExpression = new FromExpression (ExpressionHelper.CreateLambdaExpression (), ExpressionHelper.CreateParameterExpression ());
      _result.AddBodyExpression (additionalFromExpression);

      _expressionCreator.CreateQueryExpression ();
    }

    [Test]
    public void BodyExpressions_TranslatedIntoAdditionalFromClauses ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      FromExpression fromExpression1 = new FromExpression (ExpressionHelper.CreateLambdaExpression (), ExpressionHelper.CreateParameterExpression ());
      FromExpression fromExpression2 = new FromExpression (ExpressionHelper.CreateLambdaExpression (), Expression.Parameter (typeof (int), "j"));

      _result.AddBodyExpression (fromExpression1);
      _result.AddBodyExpression (fromExpression2);

      QueryExpression expression = _expressionCreator.CreateQueryExpression ();     
      
      Assert.AreEqual (2, expression.BodyClauses.Count);

      AdditionalFromClause additionalFromClause1 = expression.BodyClauses.First() as AdditionalFromClause;
      Assert.IsNotNull (additionalFromClause1);
      Assert.AreSame (fromExpression1.Expression, additionalFromClause1.FromExpression);
      Assert.AreSame (fromExpression1.Identifier, additionalFromClause1.Identifier);
      Assert.AreSame (_result.ProjectionExpressions[0], additionalFromClause1.ProjectionExpression);

      AdditionalFromClause additionalFromClause2 = expression.BodyClauses.Last () as AdditionalFromClause;
      Assert.IsNotNull (additionalFromClause2);
      Assert.AreSame (fromExpression2.Expression, additionalFromClause2.FromExpression);
      Assert.AreSame (fromExpression2.Identifier, additionalFromClause2.Identifier);
      Assert.AreSame (_result.ProjectionExpressions[1], additionalFromClause2.ProjectionExpression);
    }

    [Test]
    public void BodyExpressions_TranslatedIntoSubQueryFromClauses ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      IQueryable<Student> subQuery = SelectTestQueryGenerator.CreateSimpleQuery (ExpressionHelper.CreateQuerySource());
      FromExpression fromExpression1 = new FromExpression (Expression.Lambda (subQuery.Expression, ExpressionHelper.CreateParameterExpression()),
          ExpressionHelper.CreateParameterExpression());

      _result.AddBodyExpression (fromExpression1);

      QueryExpression expression = _expressionCreator.CreateQueryExpression();

      Assert.AreEqual (1, expression.BodyClauses.Count);

      SubQueryFromClause subQueryFromClause1 = expression.BodyClauses[0] as SubQueryFromClause;
      Assert.IsNotNull (subQueryFromClause1);
      Assert.AreSame (subQuery.Expression, subQueryFromClause1.SubQueryExpression.GetExpressionTree());
      Assert.AreSame (fromExpression1.Identifier, subQueryFromClause1.Identifier);
      Assert.AreSame (_result.ProjectionExpressions[0], subQueryFromClause1.ProjectionExpression);
    }

    [Test]
    public void BodyExpressions_TranslatedIntoSubQueryFromClauses_WithCorrectParent ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());

      IQueryable<Student> subQuery = SelectTestQueryGenerator.CreateSimpleQuery (ExpressionHelper.CreateQuerySource ());
      FromExpression fromExpression1 = new FromExpression (Expression.Lambda (subQuery.Expression, ExpressionHelper.CreateParameterExpression ()),
          ExpressionHelper.CreateParameterExpression ());

      _result.AddBodyExpression (fromExpression1);

      QueryExpression expression = _expressionCreator.CreateQueryExpression ();

      SubQueryFromClause subQueryFromClause1 = (SubQueryFromClause) expression.BodyClauses[0];
      Assert.AreSame (expression, subQueryFromClause1.SubQueryExpression.ParentQuery);
    }

    [Test]
    public void LastProjectionExpresion_TranslatedIntoSelectClause_WithFromClauses ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      FromExpression fromExpression = new FromExpression (ExpressionHelper.CreateLambdaExpression(), ExpressionHelper.CreateParameterExpression());

      _result.AddBodyExpression (fromExpression);

      QueryExpression expression = _expressionCreator.CreateQueryExpression ();
      
      Assert.AreEqual (1, expression.BodyClauses.Count);

      SelectClause selectClause = expression.SelectOrGroupClause as SelectClause;
      Assert.IsNotNull (selectClause);
      Assert.AreSame (_result.ProjectionExpressions[1], selectClause.ProjectionExpression);
    }

    [Test]
    public void BodyExpression_TranslatedIntoWhereClause ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      WhereExpression whereExpression = new WhereExpression (ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (whereExpression);

      QueryExpression expression = _expressionCreator.CreateQueryExpression ();

      Assert.AreEqual (1, expression.BodyClauses.Count);

      WhereClause whereClause = expression.BodyClauses.First() as WhereClause;
      Assert.IsNotNull (whereClause);
      Assert.AreSame (whereExpression.Expression, whereClause.BoolExpression);
    }

    [Test]
    public void BodyExpression_TranslatedIntoOrderByClause ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      OrderExpression orderExpression = new OrderExpression (true, OrderDirection.Asc, ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (orderExpression);

      QueryExpression expression = _expressionCreator.CreateQueryExpression ();
      
      Assert.AreEqual (1, expression.BodyClauses.Count);

      OrderByClause orderByClause = expression.BodyClauses.First() as OrderByClause;
      Assert.IsNotNull (orderByClause);
      Assert.AreEqual (1, orderByClause.OrderingList.Count);
      Assert.AreSame (orderExpression.Expression, orderByClause.OrderingList.First().Expression);
      Assert.AreEqual (orderExpression.OrderDirection, orderByClause.OrderingList.First().OrderDirection);
      Assert.AreSame (expression.MainFromClause, orderByClause.OrderingList.First().PreviousClause);
    }

    [Test]
    public void OrderByThenBy_TranslatedIntoOrderByClause ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      OrderExpression orderExpression1 = new OrderExpression (true, OrderDirection.Asc, ExpressionHelper.CreateLambdaExpression());
      OrderExpression orderExpression2 = new OrderExpression (false, OrderDirection.Desc, ExpressionHelper.CreateLambdaExpression());
      OrderExpression orderExpression3 = new OrderExpression (true, OrderDirection.Asc, ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (orderExpression1);
      _result.AddBodyExpression (orderExpression2);
      _result.AddBodyExpression (orderExpression3);

      QueryExpression expression = _expressionCreator.CreateQueryExpression();
      
      
      Assert.AreEqual (2, expression.BodyClauses.Count);

      OrderByClause orderByClause1 = expression.BodyClauses.First() as OrderByClause;
      OrderByClause orderByClause2 = expression.BodyClauses.Last() as OrderByClause;

      Assert.IsNotNull (orderByClause1);
      Assert.IsNotNull (orderByClause2);

      Assert.AreEqual (2, orderByClause1.OrderingList.Count);
      Assert.AreEqual (1, orderByClause2.OrderingList.Count);

      Assert.AreSame (orderExpression1.Expression, orderByClause1.OrderingList.First().Expression);
      Assert.AreEqual (orderExpression1.OrderDirection, orderByClause1.OrderingList.First().OrderDirection);
      Assert.AreSame (expression.MainFromClause, orderByClause1.OrderingList.First().PreviousClause);
      Assert.AreSame (orderExpression2.Expression, orderByClause1.OrderingList.Last().Expression);
      Assert.AreEqual (orderExpression2.OrderDirection, orderByClause1.OrderingList.Last().OrderDirection);
      Assert.AreSame (orderByClause1, orderByClause1.OrderingList.Last().PreviousClause);
      Assert.AreSame (orderExpression3.Expression, orderByClause2.OrderingList.First().Expression);
      Assert.AreEqual (orderExpression3.OrderDirection, orderByClause2.OrderingList.First().OrderDirection);
      Assert.AreSame (orderByClause1, orderByClause2.OrderingList.First().PreviousClause);
    }

    [Test]
    public void MultiExpression_IntegrationTest ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      FromExpression fromExpression1 = new FromExpression (ExpressionHelper.CreateLambdaExpression(), Expression.Parameter (typeof (Student), "s1"));
      FromExpression fromExpression2 = new FromExpression (ExpressionHelper.CreateLambdaExpression(), Expression.Parameter (typeof (Student), "s2"));
      WhereExpression whereExpression1 = new WhereExpression (ExpressionHelper.CreateLambdaExpression());
      WhereExpression whereExpression2 = new WhereExpression (ExpressionHelper.CreateLambdaExpression());

      OrderExpression orderExpression1 = new OrderExpression (true, OrderDirection.Asc, ExpressionHelper.CreateLambdaExpression());
      OrderExpression orderExpression2 = new OrderExpression (false, OrderDirection.Desc, ExpressionHelper.CreateLambdaExpression());
      OrderExpression orderExpression3 = new OrderExpression (true, OrderDirection.Asc, ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (fromExpression1);
      _result.AddBodyExpression (fromExpression2);
      _result.AddBodyExpression (whereExpression1);
      _result.AddBodyExpression (whereExpression2);
      _result.AddBodyExpression (orderExpression1);
      _result.AddBodyExpression (orderExpression2);
      _result.AddBodyExpression (orderExpression3);


      QueryExpression expression = _expressionCreator.CreateQueryExpression ();

      OrderByClause orderByClause1 = expression.BodyClauses.Skip (4).First() as OrderByClause;
      OrderByClause orderByClause2 = expression.BodyClauses.Skip (5).First () as OrderByClause;

      AdditionalFromClause fromClause1 = expression.BodyClauses.First () as AdditionalFromClause;
      Assert.IsNotNull (fromClause1);
      Assert.AreSame (fromExpression1.Identifier, fromClause1.Identifier);
      Assert.AreSame (fromExpression1.Expression, fromClause1.FromExpression);
      Assert.AreSame (_result.ProjectionExpressions[0], fromClause1.ProjectionExpression);
      Assert.AreSame (expression.MainFromClause, fromClause1.PreviousClause);

      AdditionalFromClause fromClause2 = expression.BodyClauses.Skip (1).First () as AdditionalFromClause;
      Assert.IsNotNull (fromClause2);
      Assert.AreSame (fromExpression2.Identifier, fromClause2.Identifier);
      Assert.AreSame (fromExpression2.Expression, fromClause2.FromExpression);
      Assert.AreSame (_result.ProjectionExpressions[1], fromClause2.ProjectionExpression);
      Assert.AreSame (fromClause1, fromClause2.PreviousClause);

      WhereClause whereClause1 = expression.BodyClauses.Skip (2).First () as WhereClause;
      Assert.IsNotNull (whereClause1);
      Assert.AreSame (whereExpression1.Expression, whereClause1.BoolExpression);
      Assert.AreSame (fromClause2, whereClause1.PreviousClause);

      WhereClause whereClause2 = expression.BodyClauses.Skip (3).First () as WhereClause;
      Assert.IsNotNull (whereClause2);
      Assert.AreSame (whereExpression2.Expression, whereClause2.BoolExpression);
      Assert.AreSame (whereClause1, whereClause2.PreviousClause);

      Assert.IsNotNull (orderByClause1);
      Assert.AreSame (orderExpression1.Expression, orderByClause1.OrderingList.First().Expression);
      Assert.AreSame (whereClause2, orderByClause1.OrderingList.First().PreviousClause);
      Assert.AreSame (whereClause2, orderByClause1.PreviousClause);

      Assert.AreSame (orderExpression2.Expression, orderByClause1.OrderingList.Last().Expression);
      Assert.AreSame (orderByClause1, orderByClause1.OrderingList.Last().PreviousClause);


      Assert.IsNotNull (orderByClause2);
      Assert.AreSame (orderExpression3.Expression, orderByClause2.OrderingList.First().Expression);
      Assert.AreSame (orderByClause1, orderByClause2.OrderingList.First().PreviousClause);
      Assert.AreSame (orderByClause1, orderByClause2.PreviousClause);

      SelectClause selectClause = expression.SelectOrGroupClause as SelectClause;
      Assert.IsNotNull (selectClause);
      Assert.AreSame (_result.ProjectionExpressions[2], selectClause.ProjectionExpression);
      Assert.AreSame (orderByClause2, selectClause.PreviousClause);
    }
  }
}