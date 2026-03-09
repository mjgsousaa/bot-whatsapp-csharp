
function getPhoneNumber() {
    try {
        // Tenta encontrar o cabeçalho do chat onde o nome/número aparece
        const header = document.querySelector('header');
        if (!header) return "Desconhecido";

        // No WhatsApp Web, o número muitas vezes está no título ou subtexto do cabeçalho
        // quando não é um contato salvo. Se for contato salvo, o número pode estar no painel de info.
        
        // Estratégia 1: Tentar pegar do atributo title de elementos de texto no header
        const titleElements = header.querySelectorAll('[title]');
        for (let el of titleElements) {
            const title = el.getAttribute('title');
            if (title && /^\+?[\d\s\-()]+$/.test(title.trim())) {
                return title.trim();
            }
        }

        // Estratégia 2: Procurar por spans que contenham números formatados
        const spans = header.querySelectorAll('span');
        for (let span of spans) {
            const text = span.innerText;
            if (text && /^\+?[\d\s\-()]{8,}$/.test(text.trim())) {
                return text.trim();
            }
        }

        // Estratégia 3: Se for contato salvo, o número não aparece no header. 
        // Poderíamos abrir o painel de info, mas isso é lento.
        // Vamos tentar pegar do atributo data-testid ou similar se disponível.
        
        return "Contato Salvo";
    } catch (e) {
        return "Erro ao extrair";
    }
}
return getPhoneNumber();
