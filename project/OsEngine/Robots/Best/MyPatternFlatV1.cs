using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.Tab.Internal;
using OsEngine.Journal;

namespace OsEngine.Robots.Best
{
    [Bot("MyPatternFlatV1")]
    internal class MyPatternFlatV1 : BotPanel
    {
        #region Comment
        #endregion
        
        #region Parametrs
        private BotTabSimple _tabVirt;
        private BotTabSimple _tabReal;
        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterInt PinBarBody;
        private StrategyParameterInt VolumeForBot;
        private StrategyParameterInt PercDepoForBot;
        private StrategyParameterDecimal PercentPriceForStop;
        private StrategyParameterString ChangeSadeTrade;
        private StrategyParameterInt AnalisesNumberCandles;
        private StrategyParameterString TradeAllOrOnePattern;
        private StrategyParameterString PinBar;
        private StrategyParameterInt PinBarTailBody;
        private StrategyParameterInt PinBarTwoTail;
        private StrategyParameterInt AnalizeOneOrTwoCandleForStop;
        private StrategyParameterString InsiteBar;
        private StrategyParameterString PprBar;
        private string StatusTrend = "above";
        private decimal HighExtreme = decimal.MinValue;
        private decimal LowExtreme = decimal.MaxValue;
        private bool StatusTradeReal = false;
        // Indicator setting 
        private StrategyParameterDecimal Volume;
        private StrategyParameterDecimal EnvelopDeviation;
        private StrategyParameterInt EnvelopMovingLength;
        

        // Indicator
        private Envelops _envelop;
        #endregion
        public MyPatternFlatV1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);
            _tabVirt = TabsSimple[0];
            _tabReal = TabsSimple[1];

            Regime = CreateParameter("Regime", "On", new[] { "Off", "On" });
            
            // Сколько денег выделяется на бота, хз может не нужно, пока так
            VolumeForBot = CreateParameter("VolumeForBot", 100, 1, 500, 1);
            // какой % от показателя выше используется в торговле
            PercDepoForBot = CreateParameter("PercDepoForBot", 10, 1, 100, 1);
            // сколько свечей анализируется для поиска экстремума для выставления стопа
            AnalizeOneOrTwoCandleForStop = CreateParameter("AnalizeOneOrTwoCandleForStop", 10, 1, 2, 1);
            // на сколько % от экстремума выставляется стоп
            PercentPriceForStop = CreateParameter("PercentPriceForStop", 0.1m, 0.1m, 50, 0.1m);
            // Выбор направления торговли
            ChangeSadeTrade = CreateParameter("ChangeSadeTrade", "AllTrade", new[] { "AllTrade", "OnlyLong", "OnlyShort" });
            // сколько свечей назад может быть закрытие свечи за границей канала
            AnalisesNumberCandles = CreateParameter("AnalisesNumberCandles", 2, 1, 5, 1);
            // выбор торговать все подряд паттены ( в тч один но можно несколько входов ) или только один паттерн до результата
            TradeAllOrOnePattern = CreateParameter("TradeAllOrOnePattern", "AllPattern", new[] { "AllPattern", "OnePattern" });
            // паттерн пинбар включение
            PinBar = CreateParameter("PinBar", "On", new[] { "Off", "On" });
            // длинный хвост больше тела в PinBarTailBody раз
            PinBarTailBody = CreateParameter("PinBarTailBody", 2, 0, 20, 1);
            // тело свечи не меньше 5 минимальных шагов цены
            PinBarBody = CreateParameter("PinBarBody", 5, 0, 20, 1);
            // отношение хвостов пинбара, длинный должен быть в Х раз больше короткого
            PinBarTwoTail = CreateParameter("PinBarTwoTail", 2, 2, 5, 1);
            // паттерн InsiteBar включение
            InsiteBar = CreateParameter("InsiteBar", "On", new[] { "Off", "On" });
            // паттерн PprBar  включение
            PprBar = CreateParameter("PprBar", "On", new[] { "Off", "On" });
            // Индикатор
            EnvelopDeviation = CreateParameter("Envelop Deviation", 2.0m, 0.3m, 4, 0.3m);
            EnvelopMovingLength = CreateParameter("Envelop Moving Length", 10, 10, 200, 5);

            _tabVirt.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tabVirt.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tabVirt.PositionClosingSuccesEvent += _tabVirt_PositionClosingSuccesEvent;
            
            _envelop = new Envelops(name + "Envelop", false);
            _envelop = (Envelops)_tabVirt.CreateCandleIndicator(_envelop, "Prime");
            _envelop.Save();
            _envelop.Deviation = EnvelopDeviation.ValueDecimal;
            _envelop.MovingAverage.Lenght = EnvelopMovingLength.ValueInt;

            ParametrsChangeByUser += EnvelopTrend_ParametrsChangeByUser;

            DisableBotManualControl();
        }
        

        #region Servises
        public override string GetNameStrategyType()
        {
            return "MyPatternFlatV1";
        }
        public override void ShowIndividualSettingsDialog()
        {
            
        }
        /// <summary>
        /// При изменении параметров индикатора пользователем - сохранение
        /// </summary>
        private void EnvelopTrend_ParametrsChangeByUser()
        {
            _envelop.Deviation = EnvelopDeviation.ValueDecimal;
            _envelop.MovingAverage.Lenght = EnvelopMovingLength.ValueInt;
            _envelop.Reload();
        }
        /// <summary>
        /// отключить ручное сопровождение позиции
        /// </summary>
        private void DisableBotManualControl()
        {
            BotManualControl controllerVirt = _tabVirt.ManualPositionSupport;
            controllerVirt.StopIsOn = false;
            controllerVirt.ProfitIsOn = false;
            controllerVirt.DoubleExitIsOn = false;
            controllerVirt.SetbackToOpenIsOn = false;
            controllerVirt.SetbackToCloseIsOn = false;
            controllerVirt.SecondToOpenIsOn = false;
            controllerVirt.SecondToCloseIsOn = false;
            BotManualControl controllerReal = _tabReal.ManualPositionSupport;
            controllerReal.StopIsOn = false;
            controllerReal.ProfitIsOn = false;
            controllerReal.DoubleExitIsOn = false;
            controllerReal.SetbackToOpenIsOn = false;
            controllerReal.SetbackToCloseIsOn = false;
            controllerReal.SecondToOpenIsOn = false;
            controllerReal.SecondToCloseIsOn = false;
        }

        #endregion


        /// <summary>
        /// обработка событий на закрытии каждой свечи
        /// </summary>
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // Работать, если включено
            if (Regime.ValueString == "Off")
            {
                return;
            }
            // Проверка, чтобы было достаточно свеч
            if (candles.Count - 5 < _envelop.MovingAverage.Lenght)
            {
                return;
            }

            // определение статуса (над/под каналом) и экстремумов
            StatusTrendUpOrDownChanel(candles);

            List<Position> openPositions = _tabVirt.PositionsOpenAll;

            // Сначала логика закрытия
            if (openPositions != null && openPositions.Count != 0)
            {
                int numberPos = 0;
                for (int i = 0; i < openPositions.Count; i++)
                {
                    if (openPositions[i].State == PositionStateType.Open ||
                        openPositions[i].State == PositionStateType.Closing)
                    {
                        numberPos++;
                    }
                }
                if (numberPos >= 1)
                {
                    // Логика закрытия позиции по профиту
                    LogicClosePositionProfit();

                    // вероятно не правильно что сначала выставляется профит
                    LogicClosePosForStop(candles);
                }
            }

            // Если нет открытых позиций, логика входа
            if (TradeAllOrOnePattern.ValueString == "OnePattern")
            {
                if (openPositions == null || openPositions.Count == 0)
                    LogicOpenPosition(candles);
            }
            else if (TradeAllOrOnePattern.ValueString == "AllPattern")
            {
                LogicOpenPosition(candles);
            }
        }


        /// <summary>
        ///Определение статуса тренда - над или под каналом
        /// </summary>
        private void StatusTrendUpOrDownChanel(List<Candle> candles)
        {
            if (StatusTrend == "above") //над
            {
                if (candles[candles.Count - 1].High > HighExtreme)
                {
                    HighExtreme = Math.Max(candles[candles.Count - 1].High, candles[candles.Count - 2].High);
                }
                if (candles[candles.Count - 1].Low < _envelop.ValuesDown[_envelop.ValuesDown.Count - 1]) 
                {
                    StatusTrend = "under";
                    LowExtreme = candles[candles.Count - 1].High;

                    // логика чтобы отменять заявки когда неt ткрытых позиций
                    List<Position> openPositions = _tabVirt.PositionsOpenAll;
                    int step = 0;
                    for (int pos = 0; pos < openPositions.Count; pos++)
                    {
                        if (openPositions[pos].State == PositionStateType.Open ||
                        openPositions[pos].State == PositionStateType.Closing)
                        {
                            step++;
                        }
                    }
                    if (step == 0)
                    {
                        _tabVirt.CloseAllOrderInSystem();
                        if (StatusTradeReal)
                            _tabReal.CloseAllOrderInSystem();
                    }
                    
                }
            }
            else if (StatusTrend == "under")
            {
                if (candles[candles.Count - 1].Low < LowExtreme)
                {
                    LowExtreme = Math.Min(candles[candles.Count - 1].Low, candles[candles.Count - 2].Low); 
                }
                if (candles[candles.Count - 1].High > _envelop.ValuesUp[_envelop.ValuesUp.Count - 1])
                {
                    StatusTrend = "above";
                    HighExtreme = candles[candles.Count - 1].Low;

                    List<Position> openPositions = _tabVirt.PositionsOpenAll;
                    int step = 0;
                    for (int pos = 0; pos < openPositions.Count; pos++)
                    {
                        if (openPositions[pos].State == PositionStateType.Open ||
                        openPositions[pos].State == PositionStateType.Closing)
                        {
                            step++;
                        }
                    }
                    if (step == 0)
                    {
                        _tabVirt.CloseAllOrderInSystem();
                        if (StatusTradeReal)
                            _tabReal.CloseAllOrderInSystem();
                    }
                }
            }
            
        }


        /// <summary>
        /// логика расчета и выставления закрытия позиции по профиту
        /// </summary>
        /// <param name="candles"></param>
        private void LogicClosePositionProfit()
        {
            List<Position> openPositions = _tabVirt.PositionsOpenAll;

            // лимитка на противоположную границу
            if (openPositions[0].Direction == Side.Buy)
            {
                for (int pos = 0; pos < openPositions.Count; pos++)
                {
                    _tabVirt.CloseAtLimit(openPositions[pos],
                        _envelop.ValuesUp[_envelop.ValuesUp.Count - 1],
                        openPositions[pos].OpenVolume, "CloseBuy_");
                }
            }
            else if (openPositions[0].Direction == Side.Sell)
            {
                for (int pos = 0; pos < openPositions.Count; pos++)
                {
                    _tabVirt.CloseAtLimit(openPositions[pos],
                        _envelop.ValuesDown[_envelop.ValuesDown.Count - 1],
                        openPositions[pos].OpenVolume, "CloseSell_");
                }
            }            
        }


        /// <summary>
        /// логика закрытия позиции по стопу, в зависимости от количества открытых поз
        /// </summary>
        /// 
        private void LogicClosePosForStop(List<Candle> candles)
        {
            List<Position> openPositions = _tabVirt.PositionsOpenAll;

            decimal priceExtremum;

            for (int pos = 0; pos < openPositions.Count; pos++)
            {
                if (openPositions[pos].Direction == Side.Sell)
                {
                    priceExtremum = CalculateExtremumPrice("Sell", candles, openPositions[pos]);

                    _tabVirt.CloseAtStopMarket(openPositions[pos], priceExtremum);
                }
                else if (openPositions[pos].Direction == Side.Buy)
                {
                    priceExtremum = CalculateExtremumPrice("Buy", candles, openPositions[pos]);

                    _tabVirt.CloseAtStopMarket(openPositions[pos], priceExtremum);
                }
            }            
        }


        /// <summary>
        /// Расчет экстремального значения для выставления стопа/профита
        /// </summary>
        private decimal CalculateExtremumPrice(string SadeEntry, List<Candle> candles, Position pos)
        {
            decimal priceExtremum = 0;

            if (SadeEntry == "Sell")
            {
                if (AnalizeOneOrTwoCandleForStop.ValueInt >= 10)
                {
                    priceExtremum = HighExtreme;
                }
                else
                {
                    for (int numCandle = candles.Count - 1; numCandle > 0; numCandle--)
                    {
                        if (candles[numCandle].TimeStart == pos.TimeOpen)
                        {
                            priceExtremum = candles[numCandle].High;
                            for (int numCandleNew = numCandle; 
                                numCandleNew > numCandle - AnalizeOneOrTwoCandleForStop.ValueInt; 
                                numCandleNew--)
                            {
                                priceExtremum = Math.Max(candles[numCandle - 1].High, priceExtremum);
                            }
                        }
                    }
                }
                /*// если анализируется две свечи (свеча входа и предыдущая)
                if (AnalizeOneOrTwoCandleForStop.ValueInt == 2)
                {
                    for (int numCandle = candles.Count - 1; numCandle > 0; numCandle--)
                    {
                        // нашли свечу входа, выбираем из 2 экстремумов
                        if (candles[numCandle].TimeStart == pos.TimeOpen)
                        {
                            priceExtremum = Math.Max(candles[numCandle - 1].High, candles[numCandle - 2].High);
                        }
                    }
                }
                else
                {
                    for (int numCandle = candles.Count - 1; numCandle > 0; numCandle--)
                    {
                        // нашли свечу входа, берем ее экстремум
                        if (candles[numCandle].TimeStart == pos.TimeOpen)
                        {
                            priceExtremum = candles[numCandle - 1].High;
                        }
                    }
                }*/
                // добавляется к эксремуму значение отступа
                priceExtremum += priceExtremum / 100 * PercentPriceForStop.ValueDecimal;
            }
            else if (SadeEntry == "Buy")
            {
                if (AnalizeOneOrTwoCandleForStop.ValueInt >= 10)
                {
                    priceExtremum = LowExtreme;
                }
                else
                {
                    for (int numCandle = candles.Count - 1; numCandle > 0; numCandle--)
                    {
                        if (candles[numCandle].TimeStart == pos.TimeOpen)
                        {
                            priceExtremum = candles[numCandle].Low;
                            for (int numCandleNew = numCandle;
                                numCandleNew > numCandle - AnalizeOneOrTwoCandleForStop.ValueInt;
                                numCandleNew--)
                            {
                                priceExtremum = Math.Min(candles[numCandle - 1].Low, priceExtremum);
                            }
                        }
                    }
                }
                /*    // если анализируется две свечи (свеча входа и предыдущая)
                    if (AnalizeOneOrTwoCandleForStop.ValueInt == 2)
                {
                    for (int numCandle = candles.Count - 1; numCandle > 0; numCandle--)
                    {
                        // нашли свечу входа, берем ее экстремум
                        if (candles[numCandle].TimeStart == pos.TimeOpen)
                        {
                            priceExtremum = Math.Min(candles[numCandle - 1].Low, candles[numCandle - 2].Low);
                        }
                    }
                }
                else
                {
                    for (int numCandle = candles.Count - 1; numCandle > 0; numCandle--)
                    {
                        if (candles[numCandle].TimeStart == pos.TimeOpen)
                        {
                            priceExtremum = candles[numCandle - 1].Low;
                        }
                    }
                }*/

                priceExtremum -= priceExtremum / 100 * PercentPriceForStop.ValueDecimal;
            }

            return priceExtremum;
        }


        /// <summary>
        /// сколько свечей анализировать , которые находятся за границей канала
        /// </summary>
        private (bool, string) AnalisesNumberCandlesBelowPattern(List<Candle> candles)
        {
            int stepSell = 0;
            int stepBuy = 0;
            int stopAnaliz = candles.Count - 2 - AnalisesNumberCandles.ValueInt;
            bool res = false;
            string sideTrade = "";
            for (int numCandle = candles.Count - 2; numCandle > stopAnaliz; numCandle--)
            {
                if (candles[numCandle].Close > _envelop.ValuesUp[numCandle])
                    stepSell++;
                else if (candles[numCandle].Close < _envelop.ValuesDown[numCandle])
                    stepBuy ++;
            }

            if (stepSell >= 1)
            {
                res = true;
                sideTrade = "Sell";
            }
            else if (stepBuy >= 1)
            {
                res = true;
                sideTrade = "Buy";
            }
            return (res, sideTrade);
        }


        /// <summary>
        /// Logic open position
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 2].Close;

            if (ChangeSadeTrade.ValueString != "OnlyLong")
            {
                // есть ли свечи, которые за границей канала
                var resultAnalises = AnalisesNumberCandlesBelowPattern(candles);
                if (resultAnalises.Item1 && resultAnalises.Item2 == "Sell")
                {
                    // есть ли паттерн
                    var result = ReserchePattern(candles, "Sell");
                    if (result.Item1)
                    {
                        if (result.Item2 == "PinBar")
                        {
                            _tabVirt.SellAtMarket(ActionVolForTrade(candles), result.Item2);
                            if (StatusTradeReal)
                                _tabReal.SellAtMarket(ActionVolForTrade(candles), result.Item2);
                        }

                        else if (result.Item2 == "InsiteBar")
                        {
                            _tabVirt.SellAtStop(ActionVolForTrade(candles),
                                candles[candles.Count - 1].Low - 2 * _tabVirt.Securiti.PriceStep,
                                candles[candles.Count - 1].Low,
                                StopActivateType.LowerOrEqyal, 2, result.Item2);
                            if (StatusTradeReal)
                                _tabReal.SellAtStop(ActionVolForTrade(candles),
                                candles[candles.Count - 1].Low - 2 * _tabVirt.Securiti.PriceStep,
                                candles[candles.Count - 1].Low,
                                StopActivateType.LowerOrEqyal, 2, result.Item2);
                        }

                        else if (result.Item2 == "PprBar")
                        {
                            _tabVirt.SellAtLimit(ActionVolForTrade(candles),
                                candles[candles.Count - 2].Low,
                                result.Item2);
                            if (StatusTradeReal)
                                _tabReal.SellAtLimit(ActionVolForTrade(candles),
                                candles[candles.Count - 2].Low,
                                result.Item2);
                        }
                    }
                }   
            }

            if (ChangeSadeTrade.ValueString != "OnlyShort")
            {
                var resultAnalises = AnalisesNumberCandlesBelowPattern(candles);
                if (resultAnalises.Item1 && resultAnalises.Item2 == "Buy")
                {
                    var result = ReserchePattern(candles, "Buy");
                    if (result.Item1)
                    {
                        if (result.Item2 == "PinBar")
                        {
                            _tabVirt.BuyAtMarket(ActionVolForTrade(candles), result.Item2);
                            if (StatusTradeReal)
                                _tabReal.BuyAtMarket(ActionVolForTrade(candles), result.Item2);
                        }

                        else if (result.Item2 == "InsiteBar")
                        {
                            _tabVirt.BuyAtStop(ActionVolForTrade(candles),
                                candles[candles.Count - 1].High - 2 * _tabVirt.Securiti.PriceStep,
                                candles[candles.Count - 1].High,
                                StopActivateType.HigherOrEqual, 2, result.Item2);
                            if (StatusTradeReal)
                                _tabReal.BuyAtStop(ActionVolForTrade(candles),
                                candles[candles.Count - 1].High - 2 * _tabVirt.Securiti.PriceStep,
                                candles[candles.Count - 1].High,
                                StopActivateType.HigherOrEqual, 2, result.Item2);
                        }

                        else if (result.Item2 == "PprBar")
                        {
                            _tabVirt.BuyAtLimit(ActionVolForTrade(candles),
                                candles[candles.Count - 2].High, 
                                result.Item2);
                            if (StatusTradeReal)
                                _tabReal.BuyAtLimit(ActionVolForTrade(candles),
                                candles[candles.Count - 2].High,
                                result.Item2);
                        }
                    }
                }                
            }
        }


        /// <summary>
        /// поиск паттeрна и вход по нему
        /// </summary>
        private (bool, string) ReserchePattern(List<Candle> candles, string SadeEntry)
        {
            bool res = false;
            string pattern = "";
            if (PinBar.ValueString == "On")
            {
                if (SadeEntry == "Sell")
                {
                    // хвост сверху больше тела в Х раз
                    if(candles[candles.Count - 1].ShadowTop / PinBarTailBody.ValueInt > candles[candles.Count - 1].Body &&
                        // тело больше PinBarBody.ValueInt
                        candles[candles.Count - 1].Body >= PinBarBody.ValueInt * _tabVirt.Securiti.PriceStep &&
                        // хвост сверху в PinBarTwoTail раз больше хвоста снизу
                        candles[candles.Count - 1].ShadowTop / PinBarTwoTail.ValueInt  > candles[candles.Count - 1].ShadowBottom)
                    {
                        res = true;
                        pattern = "PinBar";
                    }
                }

                else if (SadeEntry == "Buy")
                {
                    if(candles[candles.Count - 1].ShadowBottom / PinBarTailBody.ValueInt > candles[candles.Count - 1].Body &&
                        candles[candles.Count - 1].Body >= PinBarBody.ValueInt * _tabVirt.Securiti.PriceStep &&
                        candles[candles.Count - 1].ShadowBottom / PinBarTwoTail.ValueInt > candles[candles.Count - 1].ShadowTop)
                    {
                        res = true;
                        pattern = "PinBar";
                    }
                }
            }

            if (InsiteBar.ValueString == "On")
            {
                if (SadeEntry == "Sell")
                {
                    if (candles[candles.Count - 1].High < candles[candles.Count - 2].High &&
                        candles[candles.Count - 1].Low > candles[candles.Count - 2].Low)
                    {
                        res = true;
                        pattern = "InsiteBar";
                    }
                }
                else if (SadeEntry == "Buy")
                {
                    if (candles[candles.Count - 1].High < candles[candles.Count - 2].High &&
                        candles[candles.Count - 1].Low > candles[candles.Count - 2].Low)
                    {
                        res = true;
                        pattern = "InsiteBar";
                    }
                }
            }

            if (PprBar.ValueString == "On")
            {
                if (SadeEntry == "Sell")
                {
                    if (candles[candles.Count - 1].Close < candles[candles.Count - 2].Low &&
                        // Low свечи не ниже нижней границы канала
                        candles[candles.Count - 1].Low > _envelop.ValuesDown[_envelop.ValuesDown.Count - 1] &&
                        StatusTrend == "above" )
                    {
                        res = true;
                        pattern = "PprBar";
                    }
                }
                else if (SadeEntry == "Buy")
                {
                    if (candles[candles.Count - 1].Close > candles[candles.Count - 2].High &&
                        candles[candles.Count - 1].High < _envelop.ValuesUp[_envelop.ValuesUp.Count - 1] &&
                        StatusTrend == "under")
                    {
                        res = true;
                        pattern = "PprBar";
                    }
                }
            }

            return (res, pattern);
        }


        /// <summary>
        /// метод расчета объема позиции
        /// </summary>
        private decimal ActionVolForTrade(List<Candle> candles)
        {
            Security securit = _tabVirt.Securiti;
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal volumeForPos = VolumeForBot.ValueInt / 100 * PercDepoForBot.ValueInt;
            volumeForPos = Convert.ToDecimal(volumeForPos) / lastPrice;
            volumeForPos = Math.Floor(volumeForPos / securit.Lot) * securit.Lot;

            return volumeForPos;
        }


        /// <summary>
        /// обработка событий при открытии новой позиции
        /// </summary>
        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tabVirt.BuyAtStopCancel();
            _tabVirt.SellAtStopCancel();
            if (StatusTradeReal)
                _tabReal.BuyAtStopCancel();
            if (StatusTradeReal)
                _tabReal.SellAtStopCancel();
        }

        private void _tabVirt_PositionClosingSuccesEvent(Position position)
        {
            AnalizesVirtualEqtis();
        }

        /// <summary>
        /// сбор информации о закрытых позициях
        /// </summary>
        private void AnalizesVirtualEqtis()
        {
            Journal.Journal journal = _tabVirt.GetJournal();
            journal.ToString();
            List<Position> openPositions = _tabVirt.PositionsCloseAll;
        }
    }
}
