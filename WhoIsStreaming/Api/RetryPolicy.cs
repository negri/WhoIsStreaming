using System;
using System.Threading.Tasks;

namespace Negri.Twitch.Api
{
    /// <summary>
    ///     Classe para implementar retry automático de operações que podem sofrer de erros transientes
    /// </summary>
    public abstract class RetryPolicy
    {
        /// <summary>
        ///     Se a primeira tentativa após falha deve ser imediatamente executada
        /// </summary>
        private bool FastFirstRetry { get; } = true;

        /// <summary>
        ///     O numero máximo de tentativas padrão
        /// </summary>
        private int DefaultMaxTries { get; } = 5;


        /// <summary>
        ///     Executa uma função com a possibilidade de tentar novamente
        /// </summary>
        /// <typeparam name="T">O tipo do retorno da função</typeparam>
        /// <param name="func">A função</param>
        /// <param name="maxTries">Numero total de tentativas a serem feitas</param>
        /// <param name="beforeWaitAction">Função, opcional, que é chamada imediatamente antes de esperar</param>
        /// <param name="isTransientError">
        ///     Função para determinar se uma exceção é transiente ou não. Se não informado, toda
        ///     exceção é considerada transiente.
        /// </param>
        public T ExecuteAction<T>(Func<T> func, int? maxTries = null,
            Action<int, TimeSpan, Exception> beforeWaitAction = null, Func<Exception, bool> isTransientError = null)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            var effectiveMaxTries = maxTries ?? DefaultMaxTries;
            var num = 0;
            while (true)
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    if (isTransientError != null)
                    {
                        var isTransient = isTransientError(ex);
                        if (!isTransient) throw;
                    }

                    ++num;
                    if (num >= effectiveMaxTries) throw;

                    TimeSpan wait;
                    if (num == 1 && FastFirstRetry)
                        wait = TimeSpan.Zero;
                    else
                        wait = GetWaitTime(num);

                    beforeWaitAction?.Invoke(num, wait, ex);

                    Task.Delay(wait).Wait(wait);
                }
        }

        /// <summary>
        ///     Devolve o tempo de espera
        /// </summary>
        /// <param name="currentRetryNumber">O número da tentativa, a primeira vez essa função é chamada com o valor 1</param>
        /// <returns></returns>
        protected abstract TimeSpan GetWaitTime(int currentRetryNumber);
    }
}