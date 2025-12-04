using System;
using System.Threading.Tasks;
using TorrentClient.Utilities;
using Xunit;

namespace TorrentClient.Tests;

public class TaskTimeoutHelperTests
{
    /// <summary>
    /// Проверяет, что задача, завершающаяся до истечения таймаута, завершается успешно
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_TaskCompletesBeforeTimeout_CompletesSuccessfully()
    {
        // Arrange
        var task = Task.Delay(100);

        // Act
        await TaskTimeoutHelper.TimeoutAsync(task, TimeSpan.FromSeconds(1));

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
    }

    /// <summary>
    /// Проверяет, что при превышении таймаута выбрасывается исключение TimeoutException
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_TaskExceedsTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var task = Task.Delay(2000);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            TaskTimeoutHelper.TimeoutAsync(task, TimeSpan.FromMilliseconds(100)));
    }

    /// <summary>
    /// Проверяет, что исключение, выброшенное задачей, корректно пробрасывается
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_TaskThrowsException_PropagatesException()
    {
        // Arrange
        var task = Task.Run(() => throw new InvalidOperationException("Test exception"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TaskTimeoutHelper.TimeoutAsync(task, TimeSpan.FromSeconds(1)));
        Assert.Equal("Test exception", exception.Message);
    }

    /// <summary>
    /// Проверяет, что при передаче null задачи выбрасывается исключение ArgumentNullException
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_NullTask_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TaskTimeoutHelper.TimeoutAsync(null!, TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// Проверяет, что задача с результатом, завершающаяся до истечения таймаута, возвращает результат
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_WithResult_TaskCompletesBeforeTimeout_ReturnsResult()
    {
        // Arrange
        var task = Task.FromResult(42);

        // Act
        var result = await TaskTimeoutHelper.TimeoutAsync(task, TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(42, result);
    }

    /// <summary>
    /// Проверяет, что при превышении таймаута для задачи с результатом выбрасывается исключение TimeoutException
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_WithResult_TaskExceedsTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var task = Task.Run(async () =>
        {
            await Task.Delay(2000);
            return 42;
        });

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            TaskTimeoutHelper.TimeoutAsync(task, TimeSpan.FromMilliseconds(100)));
    }

    /// <summary>
    /// Проверяет, что исключение, выброшенное задачей с результатом, корректно пробрасывается
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_WithResult_TaskThrowsException_PropagatesException()
    {
        // Arrange
        var task = Task.FromException<int>(new InvalidOperationException("Test exception"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TaskTimeoutHelper.TimeoutAsync(task, TimeSpan.FromSeconds(1)));
        Assert.Equal("Test exception", exception.Message);
    }

    /// <summary>
    /// Проверяет, что при передаче null задачи с результатом выбрасывается исключение ArgumentNullException
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_WithResult_NullTask_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TaskTimeoutHelper.TimeoutAsync<int>(null!, TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// Проверяет, что ValueTask, завершающийся до истечения таймаута, завершается успешно
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_ValueTask_CompletesBeforeTimeout_CompletesSuccessfully()
    {
        // Arrange
        var valueTask = new ValueTask(Task.Delay(100));

        // Act
        await TaskTimeoutHelper.TimeoutAsync(valueTask, TimeSpan.FromSeconds(1));

        // Assert
        Assert.True(valueTask.IsCompletedSuccessfully);
    }

    /// <summary>
    /// Проверяет, что при превышении таймаута для ValueTask выбрасывается исключение TimeoutException
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_ValueTask_ExceedsTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var valueTask = new ValueTask(Task.Delay(2000));

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            TaskTimeoutHelper.TimeoutAsync(valueTask, TimeSpan.FromMilliseconds(100)));
    }

    /// <summary>
    /// Проверяет, что ValueTask с результатом, завершающийся до истечения таймаута, возвращает результат
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_ValueTaskWithResult_CompletesBeforeTimeout_ReturnsResult()
    {
        // Arrange
        var valueTask = new ValueTask<int>(Task.FromResult(42));

        // Act
        var result = await TaskTimeoutHelper.TimeoutAsync(valueTask, TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(42, result);
    }

    /// <summary>
    /// Проверяет, что при превышении таймаута для ValueTask с результатом выбрасывается исключение TimeoutException
    /// </summary>
    [Fact]
    public async Task TimeoutAsync_ValueTaskWithResult_ExceedsTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var valueTask = new ValueTask<int>(Task.Run(async () =>
        {
            await Task.Delay(2000);
            return 42;
        }));

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            TaskTimeoutHelper.TimeoutAsync(valueTask, TimeSpan.FromMilliseconds(100)));
    }
}

