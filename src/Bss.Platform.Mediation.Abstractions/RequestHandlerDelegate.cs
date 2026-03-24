namespace Bss.Platform.Mediation.Abstractions;

public delegate Task RequestHandlerDelegate();

public delegate Task<TResult> RequestHandlerDelegate<TResult>();
