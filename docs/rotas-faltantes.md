# Rotas faltantes

Este arquivo registra fluxos ainda dependentes de rotas antigas ou de contrato
novo a definir.

## Check-in

- Entrada por codigo de barras
  - Status: implementado temporariamente com rotas legadas.
  - Situacao atual: o botao executa o fluxo legado de impressao/consulta de
    resultado por codigo de barras via BFF. Quando a nova rota for criada,
    substituir a implementacao do BFF mantendo o app chamando a mesma rota
    interna, se possivel.
  - Observacao: nao foi identificada rota nova na documentacao de
    AutoAtendimento.
  - Verificacao no fonte antigo `passelivre.zip`: a rotina encontrada com
    codigo de barras esta ligada a impressao/consulta de resultado, nao ao
    pre-atendimento. O fluxo antigo usa `ResultadoPedidoBean`:
    `abrirDialgImpressaoPorCodigoBarra`, `imprimirRestultadoPorCodigoBarra`
    e `exibirDetalhesPorCodigoBarra`.
  - Endpoints legados referenciados por chave:
    `ENDPOINT_IHP_PEDIDO_ID_DETALHES` para consultar detalhes do pedido e
    `ENDPOINT_IHP_IMPRESSAO_RESULTADO` para imprimir o resultado.
  - Chaves encontradas no `config.txt` legado:
    `ENDPOINT_IHP_PEDIDO_ID_DETALHES_TOTEM=https://services-hml.grupopardini.com.br/csp/totemRest/PedidoDetalhes/?id=`
    e
    `ENDPOINT_IHP_IMPRESSAO_RESULTADO_TOTEM=https://services-hml.grupopardini.com.br/csp/totemRest/impressao/resultado/`.
  - Regra do codigo lido no legado: 14 caracteres, iniciando com `01` e
    terminando com `00`. O codigo e convertido para `unidade||pedido` na
    consulta de detalhe e para `unidade||****||pedido` na impressao.
  - Regra de liberacao: tratar pelo campo `statusExamesPedido`. Quando for
    `1`, seguir para impressao mesmo que a API retorne `error.message`.
    Quando for `0`, nao imprimir e orientar o cliente a procurar um atendente.
