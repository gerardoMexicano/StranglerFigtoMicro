using Domain.Common;

namespace UnitTests.Domain;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult_WithErrorNone()
    {
        // Act
        var result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult_WithProvidedError()
    {
        // Arrange
        var error = Error.Validation("Users.InvalidEmail", "El correo electrónico no es válido.");

        // Act
        var result = Result.Failure(error);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Users.InvalidEmail", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void GenericSuccess_ShouldReturnEncapsulatedValue()
    {
        // Arrange
        const string expectedValue = "CleanArchitecture";

        // Act
        var result = Result.Success(expectedValue);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedValue, result.Value);
    }

    [Fact]
    public void GenericFailure_AccessingValue_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var error = Error.NotFound("Reports.NotFound", "El reporte solicitado no existe.");
        var result = Result.Failure<string>(error);

        // Act & Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void ErrorFactories_ShouldAssignCorrectErrorType()
    {
        // Act
        var validationError = Error.Validation("Val.01", "Error de validación");
        var notFoundError = Error.NotFound("NF.01", "No encontrado");
        var conflictError = Error.Conflict("Conf.01", "Conflicto de estado");
        var unauthorizedError = Error.Unauthorized("Auth.01", "No autenticado");
        var forbiddenError = Error.Forbidden("Forb.01", "Sin permisos suficientes");
        var failureError = Error.Failure("Fail.01", "Fallo general");

        // Assert
        Assert.Equal(ErrorType.Validation, validationError.Type);
        Assert.Equal(ErrorType.NotFound, notFoundError.Type);
        Assert.Equal(ErrorType.Conflict, conflictError.Type);
        Assert.Equal(ErrorType.Unauthorized, unauthorizedError.Type);
        Assert.Equal(ErrorType.Forbidden, forbiddenError.Type);
        Assert.Equal(ErrorType.Failure, failureError.Type);
    }
}
